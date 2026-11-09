#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.Diagnostics;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Runtime;
using LivLiv.Unity.MediaPipe;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private struct SustainedTiming
    {
        public int frame,gc0Completed;
        public double updateMs,tickMs,reportMs,faceInclusiveMs,sourceMs,voiceMs;
    }
    [Serializable]private sealed class SustainedGap
    {
        public double elapsed,wallGapMs,diagnosticSaveMs;public int diagnosticGc0Completed;public SustainedTiming timing;
    }
    [Serializable]private sealed class SustainedPhaseMaxima
    {
        // Independent component maxima; these values need not belong to the same frame.
        public double updateMs,tickMs,reportMs,faceInclusiveMs,sourceMs,voiceMs,diagnosticSaveMs;
    }
    private sealed class SustainedPhaseState
    {
        public SustainedTiming current,last;public long began,tick,report;public int gc0;
        public bool recording;public double pendingSaveMs;public int pendingSaveGc0;
        public readonly SustainedPhaseMaxima maxima=new SustainedPhaseMaxima();
        public static double Milliseconds(long began)=>(Stopwatch.GetTimestamp()-began)*1000d/Stopwatch.Frequency;
    }
    private SustainedPhaseState sustainedPhases;
    partial void ProfileSustainedPhase(string phase)
    {
        var s=sustainedPhases;if(s==null)return;
        switch(phase)
        {
            case "update-begin":s.current=default;s.began=Stopwatch.GetTimestamp();s.gc0=GC.CollectionCount(0);break;
            case "tick-begin":s.tick=Stopwatch.GetTimestamp();break;
            case "tick-end":s.current.tickMs=SustainedPhaseState.Milliseconds(s.tick);break;
            case "report-begin":s.report=Stopwatch.GetTimestamp();break;
            case "report-end":s.current.reportMs=SustainedPhaseState.Milliseconds(s.report);break;
            case "update-end":
                s.current.updateMs=SustainedPhaseState.Milliseconds(s.began);s.current.frame=frames;s.current.gc0Completed=GC.CollectionCount(0)-s.gc0;s.last=s.current;
                if(s.recording)
                {
                    var m=s.maxima;var c=s.current;
                    m.updateMs=Math.Max(m.updateMs,c.updateMs);m.tickMs=Math.Max(m.tickMs,c.tickMs);m.reportMs=Math.Max(m.reportMs,c.reportMs);
                    m.faceInclusiveMs=Math.Max(m.faceInclusiveMs,c.faceInclusiveMs);m.sourceMs=Math.Max(m.sourceMs,c.sourceMs);m.voiceMs=Math.Max(m.voiceMs,c.voiceMs);
                }
                break;
        }
    }
    private sealed class TimedSource:INativeFaceSource,ICaptureStatus
    {
        private readonly INativeFaceSource source;private readonly SustainedPhaseState timing;
        public TimedSource(INativeFaceSource source,SustainedPhaseState timing){this.source=source;this.timing=timing;}
        public bool Ready=>source.Ready;public string Error=>(source as ICaptureStatus)?.Error;
        public bool TryRead(out Texture2D texture)
        {long at=Stopwatch.GetTimestamp();try{return source.TryRead(out texture);}finally{timing.current.sourceMs+=SustainedPhaseState.Milliseconds(at);}}
        public void Dispose()=>source.Dispose();
    }
    private sealed class TimedFace:IFaceInput,ICaptureStatus
    {
        private readonly NativeFaceInput input;private readonly SustainedPhaseState timing;
        public TimedFace(NativeFaceInput input,SustainedPhaseState timing){this.input=input;this.timing=timing;}
        public bool Ready=>input.Ready;public double Fps=>input.Fps;public bool Dark=>input.Dark;public string Error=>input.Error;
        public FaceFrame Next(double now)
        {long at=Stopwatch.GetTimestamp();try{return input.Next(now);}finally{timing.current.faceInclusiveMs+=SustainedPhaseState.Milliseconds(at);}}
        public void Dispose()=>input.Dispose();
    }
    private sealed class TimedVoice:IVoiceInput
    {
        private readonly IVoiceInput input;private readonly SustainedPhaseState timing;
        public TimedVoice(IVoiceInput input,SustainedPhaseState timing){this.input=input;this.timing=timing;}
        public double Gain{get=>input.Gain;set=>input.Gain=value;}public double Level=>input.Level;public double Vowel=>input.Vowel;
        public double Read()
        {long at=Stopwatch.GetTimestamp();try{return input.Read();}finally{timing.current.voiceMs+=SustainedPhaseState.Milliseconds(at);}}
        public void Dispose()=>input.Dispose();
    }
}
#endif
