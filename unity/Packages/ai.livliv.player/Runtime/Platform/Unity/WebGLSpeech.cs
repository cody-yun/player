#if UNITY_WEBGL && !UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using UnityEngine;

namespace LivLiv.Unity
{
    internal static class BrowserSpeechNative
    {
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Create();
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Unlock(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Running(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_StateChanges(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_HasOpenContext(int id);
        [DllImport("__Internal")] internal static extern double LivLivSpeech_Time(int id, int heard);
        [DllImport("__Internal")] internal static extern double LivLivSpeech_WallTime();
        [DllImport("__Internal")] internal static extern void LivLivSpeech_Volume(int id, double volume);
        [DllImport("__Internal")] private static extern void LivLivSpeech_Error(int id, int kind, [Out] byte[] target, int capacity);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Schedule(int id, [In] float[] samples, int n, int rate, double at);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Ended(int piece);
        [DllImport("__Internal")] internal static extern void LivLivSpeech_StopPiece(int piece);
        [DllImport("__Internal")] internal static extern void LivLivSpeech_Dispose(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Load(int id, string url, int maxBytes);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_LoadState(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_LoadWav(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_LoadRead(int id, [Out] byte[] bytes, int capacity);
        [DllImport("__Internal")] internal static extern void LivLivSpeech_LoadCancel(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_Decode(int id, [In] byte[] bytes, int n, int maxSamples);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_DecodeState(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_DecodeLength(int id);
        [DllImport("__Internal")] internal static extern int LivLivSpeech_DecodeRead(int id, [Out] float[] samples, int capacity);
        [DllImport("__Internal")] internal static extern void LivLivSpeech_DecodeCancel(int id);
        internal static string Error(int id, int kind)
        {
            var bytes = new byte[2048]; LivLivSpeech_Error(id, kind, bytes, bytes.Length);
            int end = Array.IndexOf(bytes, (byte)0); return Encoding.UTF8.GetString(bytes, 0, end < 0 ? bytes.Length : end);
        }
    }
    internal sealed class WebGLSpeechOutput : ISpeechOutput, ISpeechStateOutput
    {
        private sealed class Piece : ISpeechPiece
        {
            private int id;
            internal Piece(int id) { this.id = id; }
            public bool Ended => id == 0 || BrowserSpeechNative.LivLivSpeech_Ended(id) != 0;
            public void Dispose() { if (id != 0) { BrowserSpeechNative.LivLivSpeech_StopPiece(id); id = 0; } }
        }
        internal readonly int Id;
        internal readonly GameObject Root;
        private readonly UnitySpeechOptions options;
        private bool disposed;
        internal WebGLSpeechOutput(UnitySpeechOptions options)
        {
            this.options = options;
            if (options.mixer != null) throw new NotSupportedException("Web Audio speech cannot route through a Unity AudioMixerGroup");
            double volume = Volume(options.volume);
            Id = BrowserSpeechNative.LivLivSpeech_Create();
            Root = new GameObject("LivLiv Speech Output") { hideFlags = HideFlags.DontSave };
            UnityEngine.Object.DontDestroyOnLoad(Root);
            BrowserSpeechNative.LivLivSpeech_Volume(Id, volume);
        }
        public bool Running => !disposed && BrowserSpeechNative.LivLivSpeech_Running(Id) != 0;
        public int ConsumeStateChanges() => disposed ? 0 : BrowserSpeechNative.LivLivSpeech_StateChanges(Id);
        public bool HasOpenContext => !disposed && BrowserSpeechNative.LivLivSpeech_HasOpenContext(Id) != 0;
        public double CurrentTime => disposed ? 0 : BrowserSpeechNative.LivLivSpeech_Time(Id, 0);
        public double WallTime => BrowserSpeechNative.LivLivSpeech_WallTime();
        public double HeardTime
        {
            get
            {
                if (disposed) return 0;
                if (options.outputLatencySeconds == null) return BrowserSpeechNative.LivLivSpeech_Time(Id, 1);
                double delay = options.outputLatencySeconds();
                return CurrentTime - (double.IsNaN(delay) || double.IsInfinity(delay) ? 0 : Math.Max(0, delay));
            }
        }
        public void Unlock()
        {
            ThrowIfDisposed();
            if (BrowserSpeechNative.LivLivSpeech_Unlock(Id) == 0) throw new InvalidOperationException(BrowserSpeechNative.Error(Id, 0));
        }
        public void SetVolume(double value) { ThrowIfDisposed(); BrowserSpeechNative.LivLivSpeech_Volume(Id, Volume(value)); }
        private static double Volume(double value)
        { if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value)); return Math.Max(0, Math.Min(1, value)); }
        public ISpeechPiece Schedule(float[] samples, int rate, double at)
        {
            ThrowIfDisposed();
            int piece = BrowserSpeechNative.LivLivSpeech_Schedule(Id, samples, samples.Length, rate, at);
            if (piece == 0) throw new InvalidOperationException(BrowserSpeechNative.Error(Id, 0));
            return new Piece(piece);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            BrowserSpeechNative.LivLivSpeech_Dispose(Id); TextureDecoder.Destroy(Root);
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(WebGLSpeechOutput)); }
    }
    internal sealed class WebGLSpeechLoader : ISpeechBytesLoader
    {
        private readonly WebGLSpeechOutput output;
        private const int MaxBytes = 32 * 1024 * 1024, MaxSamples = 16 * 1024 * 1024;
        internal WebGLSpeechLoader(WebGLSpeechOutput output) { this.output = output; }
        public ISpeechIncoming Open(string url, CancellationToken token)
        { token.ThrowIfCancellationRequested(); return new Incoming(output, url, null, token); }
        public ISpeechIncoming Open(byte[] bytes, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length > MaxBytes) throw new InvalidDataException("Speech exceeds the encoded byte limit");
            return new Incoming(output, null, bytes, token); // Decode copies the WASM bytes before its first await.
        }
        private sealed class Incoming : ISpeechIncoming
        {
            private readonly WebGLSpeechOutput output;
            private readonly SpeechIncomingBuffer buffer;
            private readonly CancellationTokenSource cancel;
            private int load, decode;
            private bool disposed, finished;
            public int SampleRate => buffer.SampleRate;
            internal Incoming(WebGLSpeechOutput output, string url, byte[] bytes, CancellationToken token)
            {
                this.output = output; cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
                buffer = new SpeechIncomingBuffer(cancel.Token, MaxSamples); _ = Run(url, bytes);
            }
            public Task<float[]> NextAsync() => buffer.NextAsync();
            private async Task Run(string url, byte[] bytes)
            {
                try
                {
                    if (bytes != null) await Decode(bytes);
                    else await Fetch(url);
                    buffer.Finish();
                }
                catch (OperationCanceledException) { buffer.Dispose(); }
                catch (Exception e) { buffer.Finish(e); }
                finally { Release(); finished = true; if (disposed) cancel.Dispose(); }
            }
            private async Task Fetch(string url)
            {
                load = BrowserSpeechNative.LivLivSpeech_Load(output.Id, url, MaxBytes);
                var bytes = new byte[64 * 1024]; SpeechWavReader wav = null; MemoryStream whole = null;
                double deadline = output.WallTime + 60;
                try
                {
                    while (true)
                    {
                        cancel.Token.ThrowIfCancellationRequested();
                        if (output.WallTime > deadline) throw new TimeoutException("Speech download timed out");
                        int n;
                        while ((n = BrowserSpeechNative.LivLivSpeech_LoadRead(load, bytes, bytes.Length)) > 0)
                        {
                            if (wav == null && whole == null)
                            {
                                if (BrowserSpeechNative.LivLivSpeech_LoadWav(load) != 0) wav = new SpeechWavReader(MaxBytes);
                                else whole = new MemoryStream();
                            }
                            if (wav != null) buffer.Push(wav.Push(bytes, 0, n), wav.SampleRate);
                            else whole.Write(bytes, 0, n);
                        }
                        int state = BrowserSpeechNative.LivLivSpeech_LoadState(load);
                        if (state < 0) throw new IOException(BrowserSpeechNative.Error(load, 1));
                        if (state == 1) break;
                        await Task.Yield();
                    }
                    if (wav != null)
                    {
                        buffer.Push(wav.Push(Array.Empty<byte>(), 0, 0, true), wav.SampleRate);
                        if (wav.NeedsDecoder) await Decode(wav.DecoderBytes());
                    }
                    else await Decode(whole?.ToArray() ?? Array.Empty<byte>());
                }
                finally { wav?.Dispose(); whole?.Dispose(); }
            }
            private async Task Decode(byte[] bytes)
            {
                cancel.Token.ThrowIfCancellationRequested();
                decode = BrowserSpeechNative.LivLivSpeech_Decode(output.Id, bytes, bytes.Length, MaxSamples);
                while (BrowserSpeechNative.LivLivSpeech_DecodeState(decode) == 0) { cancel.Token.ThrowIfCancellationRequested(); await Task.Yield(); }
                cancel.Token.ThrowIfCancellationRequested();
                if (BrowserSpeechNative.LivLivSpeech_DecodeState(decode) < 0) throw new InvalidDataException(BrowserSpeechNative.Error(decode, 2));
                int n = BrowserSpeechNative.LivLivSpeech_DecodeLength(decode);
                if (n > MaxSamples) throw new InvalidDataException("Speech exceeds the decoded sample limit");
                var pcm = new float[n];
                int rate = BrowserSpeechNative.LivLivSpeech_DecodeRead(decode, pcm, n);
                if (rate <= 0) throw new InvalidDataException("Speech decoder returned no sample rate");
                buffer.Push(pcm, rate);
            }
            private void Release()
            {
                if (load != 0) { BrowserSpeechNative.LivLivSpeech_LoadCancel(load); load = 0; }
                if (decode != 0) { BrowserSpeechNative.LivLivSpeech_DecodeCancel(decode); decode = 0; }
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                cancel.Cancel(); buffer.Dispose(); Release(); if (finished) cancel.Dispose();
            }
        }
    }
}
#endif
