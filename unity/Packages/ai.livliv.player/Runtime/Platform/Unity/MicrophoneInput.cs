// Native Unity microphone boundary. WebGL uses a browser Web Audio bridge instead.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using UnityEngine;
namespace LivLiv.Unity
{
    // Reader also accepts an externally owned looping AudioClip capture for integration/testing.
    // Calls, including Dispose, belong on Unity's main thread. The position is in sample frames.
    public sealed class AudioCaptureInput : IVoiceInput,ICaptureStatus
    {
        private readonly AudioClip clip;private readonly Func<int> position;private readonly Func<double> now;
        private readonly Action release;private readonly PcmVoiceInput pcm;private readonly float[] buffer;
        private readonly Func<bool> isRecording;private string error;
        private int previous;private double last;private bool disposed;
        public AudioCaptureInput(AudioClip clip,Func<int> position,Func<double> nowMilliseconds,Action release=null,Func<bool> isRecording=null)
        {
            if(clip==null)throw new ArgumentNullException(nameof(clip));if(clip.channels!=1&&clip.channels!=2)throw new LivLivError("missing-feature","LivLiv: capture must be mono or stereo");
            this.clip=clip;this.position=position??throw new ArgumentNullException(nameof(position));now=nowMilliseconds??throw new ArgumentNullException(nameof(nowMilliseconds));this.release=release;
            this.isRecording=isRecording;
            pcm=new PcmVoiceInput(clip.frequency,now);buffer=new float[1024*clip.channels];last=now();
        }
        public string Error {get{if(!disposed&&isRecording!=null&&!isRecording()){error="Microphone capture ended; reconnect and start again";Dispose();}return error;}}
        public double Level=>disposed?0:pcm.Level;public double Vowel=>disposed?0:pcm.Vowel;public double Gain {get=>pcm.Gain;set=>pcm.Gain=value;}
        public double Read()
        {
            if(Error!=null||disposed)return 0;int end=position();if(end<0)return 0;if(end>=clip.samples)throw new InvalidOperationException("Capture position is outside its circular buffer");
            double time=now();int count=(end-previous+clip.samples)%clip.samples;
            // More than one full buffer elapsed: only the newest complete buffer remains available.
            if(time-last>=clip.samples*1000d/clip.frequency){previous=end;count=clip.samples;}last=time;
            while(count>0)
            {
                int frames=Math.Min(1024,count),elements=frames*clip.channels;
                if(!clip.GetData(new Span<float>(buffer,0,elements),previous))throw new LivLivError("invalid","LivLiv: could not read microphone PCM");
                pcm.Push(buffer,0,elements,clip.channels);previous=(previous+frames)%clip.samples;count-=frames;
            }
            return pcm.Read();
        }
        public void Dispose(){if(disposed)return;disposed=true;pcm.Dispose();release?.Invoke();}
    }
    public static class MicrophoneInput
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        private static readonly HashSet<string> owned=new HashSet<string>(StringComparer.Ordinal);
#endif
        // Call from a user-initiated action on Unity's main thread. This owns only the capture it starts.
        public static async Task<IVoiceInput> StartAsync(string device=null,CancellationToken cancellationToken=default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return await WebGLVoiceInput.StartAsync(cancellationToken);
#else
            cancellationToken.ThrowIfCancellationRequested();
            await CapturePermissions.RequestAsync(false,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if(Microphone.devices.Length==0)throw new LivLivError("invalid","LivLiv: no microphone is available");
            string key=device??"";if(owned.Contains(key)||Microphone.IsRecording(device))throw new LivLivError("invalid","LivLiv: this microphone is already recording");
            owned.Add(key);AudioClip clip=null;bool started=false;
            void Release()
            {
                try{if(started)Microphone.End(device);}
                finally{started=false;try{if(clip!=null)TextureDecoder.Destroy(clip);}finally{owned.Remove(key);}}
            }
            try
            {
                int rate=AudioSettings.outputSampleRate;clip=Microphone.Start(device,true,5,rate);started=clip!=null;
                if(clip==null)throw new LivLivError("invalid","LivLiv: microphone capture could not start");
                double deadline=Time.realtimeSinceStartupAsDouble+10;
                while(Microphone.GetPosition(device)<=0)
                {
                    cancellationToken.ThrowIfCancellationRequested();if(Time.realtimeSinceStartupAsDouble>=deadline)throw new LivLivError("invalid","LivLiv: microphone produced no samples");await Task.Yield();
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new AudioCaptureInput(clip,()=>Microphone.IsRecording(device)?Microphone.GetPosition(device):-1,()=>Time.realtimeSinceStartupAsDouble*1000,Release,()=>Microphone.IsRecording(device));
            }
            catch{Release();throw;}
#endif
        }
    }
}
