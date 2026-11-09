using System;
using System.Collections.Generic;
using LivLiv.Platform;
using LivLiv.Runtime;
using UnityEngine;
using UnityEngine.Audio;

namespace LivLiv.Unity
{
    public sealed class UnitySpeechOptions
    {
        public AudioMixerGroup mixer;
        // Unity does not expose the browser's getOutputTimestamp equivalent. Supply measured
        // device latency when available; null estimates only Unity's DSP ring-buffer delay.
        public Func<double> outputLatencySeconds;
        public double volume = 1;
        public LipSyncOptions mouth;
        public Action<Exception> onError;
        // Unity factory convenience for SpeechPlayer.onState; receives its current Running value.
        public Action<bool> onState;
    }

    // Native (PC / Android / iOS) output boundary. WebGL needs its own Web Audio bridge:
    // AudioClip.SetData/runtime PCM and autoplay must not silently become platform-dependent.
    public sealed class UnitySpeechOutput : ISpeechOutput
    {
        private sealed class Piece : ISpeechPiece
        {
            private readonly UnitySpeechOutput owner;
            private readonly double end;
            internal AudioSource source;
            private AudioClip clip;
            private bool disposed;
            public Piece(UnitySpeechOutput owner, AudioSource source, AudioClip clip, double end)
            { this.owner = owner; this.source = source; this.clip = clip; this.end = end; }
            public bool Ended => disposed || owner.CurrentTime >= end;
            public void Dispose()
            {
                if (disposed) return;
                disposed = true; owner.pieces.Remove(this);
                if (source != null)
                {
                    source.Stop(); source.clip = null;
                    TextureDecoder.Destroy(source.gameObject); source = null;
                }
                if (clip != null) { TextureDecoder.Destroy(clip); clip = null; }
            }
        }
        private readonly List<Piece> pieces = new List<Piece>();
        private readonly UnitySpeechOptions options;
        private readonly GameObject root;
        private bool disposed;
        private double volume;
        internal GameObject Root => root;
        public int ActivePieceCount => pieces.Count;
        public bool Running => !disposed && !AudioListener.pause && AudioSettings.outputSampleRate > 0;
        public double CurrentTime => AudioSettings.dspTime;
        public double WallTime => Time.realtimeSinceStartupAsDouble;
        public double OutputLatencySeconds
        {
            get
            {
                double delay;
                if (options.outputLatencySeconds != null) delay = options.outputLatencySeconds();
                else
                {
                    AudioSettings.GetDSPBufferSize(out int length, out int count);
                    delay = (double)length * count / Math.Max(1, AudioSettings.outputSampleRate);
                }
                return double.IsNaN(delay) || double.IsInfinity(delay) ? 0 : Math.Max(0, delay);
            }
        }
        public double HeardTime => CurrentTime - OutputLatencySeconds;

        public UnitySpeechOutput(UnitySpeechOptions options = null)
        {
            this.options = options ?? new UnitySpeechOptions();
            root = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            throw new PlatformNotSupportedException("LivLiv: WebGL speech requires the Web Audio output adapter");
#else
            // Validate before allocating an owned Unity object (constructors cannot be disposed
            // by their caller when they throw).
            SetVolume(this.options.volume);
            root = new GameObject("LivLiv Speech Output") { hideFlags = HideFlags.DontSave };
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(root);
#endif
        }
        public void Unlock()
        {
            ThrowIfDisposed();
            // Native output needs no gesture. Never resume a globally paused AudioListener.
        }
        public void SetVolume(double value)
        {
            ThrowIfDisposed();
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            volume = Math.Max(0, Math.Min(1, value));
            foreach (var piece in pieces) if (piece.source != null) piece.source.volume = (float)volume;
        }
        public ISpeechPiece Schedule(float[] samples, int sampleRate, double at)
        {
            ThrowIfDisposed();
            if (samples == null || samples.Length == 0) throw new ArgumentException("Expected mono PCM samples", nameof(samples));
            if (sampleRate < 3000 || sampleRate > 384000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (double.IsNaN(at) || double.IsInfinity(at) || at < 0) throw new ArgumentOutOfRangeException(nameof(at));
            foreach (float sample in samples)
                if (float.IsNaN(sample) || float.IsInfinity(sample) || sample < -1 || sample > 1)
                    throw new ArgumentException("PCM samples must be finite and within -1..1", nameof(samples));
            AudioClip clip = null; GameObject node = null;
            try
            {
                clip = AudioClip.Create("LivLiv Speech PCM", samples.Length, 1, sampleRate, false);
                if (clip == null || !clip.SetData(samples, 0)) throw new InvalidOperationException("LivLiv: could not create speech PCM buffer");
                node = new GameObject("LivLiv Speech Piece") { hideFlags = HideFlags.DontSave };
                node.transform.SetParent(root.transform, false);
                var source = node.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 0; source.dopplerLevel = 0;
                source.pitch = 1; source.ignoreListenerPause = false; source.volume = (float)volume;
                source.outputAudioMixerGroup = options.mixer; source.clip = clip;
                source.PlayScheduled(at);
                var piece = new Piece(this, source, clip, at + (double)samples.Length / sampleRate);
                pieces.Add(piece); return piece;
            }
            catch
            {
                if (node != null) TextureDecoder.Destroy(node);
                if (clip != null) TextureDecoder.Destroy(clip);
                throw;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (var piece in pieces.ToArray()) piece.Dispose();
            if (root != null) TextureDecoder.Destroy(root);
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(UnitySpeechOutput)); }
    }

    public static class UnitySpeech
    {
        public static Speech Create(IPlayerHost player, ISpeechLoader loader = null, UnitySpeechOptions options = null)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            options = options ?? new UnitySpeechOptions();
#if UNITY_WEBGL && !UNITY_EDITOR
            var output = new WebGLSpeechOutput(options);
            loader = loader ?? new WebGLSpeechLoader(output);
#else
            loader = loader ?? new UnitySpeechLoader();
            var output = new UnitySpeechOutput(options);
#endif
            try
            {
                var runtime = new SpeechPlayer(output, loader) { onError = options.onError };
                runtime.onState = () => options.onState?.Invoke(runtime.Running);
                output.Root.AddComponent<UnitySpeechPump>().Initialize(runtime);
                return Speech.Attach(player, runtime, options.mouth);
            }
            catch { output.Dispose(); throw; }
        }
    }
}
