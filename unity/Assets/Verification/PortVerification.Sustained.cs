#if !UNITY_WEBGL || UNITY_EDITOR
// CLI-only generated-input endurance diagnostic. No hardware capture, output audio, or forced GC.
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity.MediaPipe;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class SustainedRow
    {
        public string stage;public double elapsed,meanFrameMs,p95FrameMs,maxFrameMs;
        public int measuredFrames;public CaptureMemory memory;public Evidence player;
    }
    [Serializable]private sealed class SustainedReport
    {
        public string result="Running",error,platform,graphics,device,processor,operatingSystem;
        public int requestedSeconds=600,renderWidth,renderHeight,screenWidth,screenHeight,systemMemoryMiB;
        public bool sourceReleased,voiceReleased;public SustainedRow[] samples;
        public bool phaseTimings;public SustainedGap[] longFrames;
        public SustainedPhaseMaxima phaseMaxima;
    }
    private sealed class SustainedVoice:IVoiceInput
    {
        private readonly PcmVoiceInput input=new PcmVoiceInput(44100,NativeFaceInput.Now);
        private readonly float[] buffer=new float[1024];private readonly double began=NativeFaceInput.Now();
        private long written;public bool disposed;
        public double Level=>input.Level;public double Vowel=>input.Vowel;
        public double Gain{get=>input.Gain;set=>input.Gain=value;}
        public double Read()
        {
            long target=(long)((NativeFaceInput.Now()-began)*44.1);
            // Bound catch-up after a stalled frame; this is a generated source, not a recording.
            if(target-written>44100)written=target-44100;
            while(written<target)
            {
                int count=(int)Math.Min(buffer.Length,target-written);
                for(int i=0;i<count;i++)buffer[i]=(float)(.12*Math.Sin(2*Math.PI*650*(written+i)/44100));
                input.Push(buffer,0,count);written+=count;
            }
            return input.Read();
        }
        public void Dispose(){if(disposed)return;disposed=true;input.Dispose();}
    }
    private async void RunSustained(string directory)
    {
        var report=new SustainedReport{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),
            device=SystemInfo.graphicsDeviceName,processor=SystemInfo.processorType,operatingSystem=SystemInfo.operatingSystem,
            systemMemoryMiB=SystemInfo.systemMemorySize,renderWidth=target.width,renderHeight=target.height,screenWidth=Screen.width,screenHeight=Screen.height};
        var rows=new List<SustainedRow>();var frameTimes=new List<double>();var gaps=new List<SustainedGap>();SmokeSource source=null;SustainedVoice voice=null;NativeFaceInput native=null;
        double began=Time.realtimeSinceStartupAsDouble;
        void Save(string stage)
        {
            long saveAt=System.Diagnostics.Stopwatch.GetTimestamp();int gcAt=GC.CollectionCount(0);
            frameTimes.Sort();double sum=0;foreach(double value in frameTimes)sum+=value;
            rows.Add(new SustainedRow{stage=stage,elapsed=Time.realtimeSinceStartupAsDouble-began,memory=CaptureMemoryAt(rows.Count,stage),player=ReadEvidence(),
                measuredFrames=frameTimes.Count,meanFrameMs=frameTimes.Count==0?0:sum/frameTimes.Count,
                p95FrameMs=frameTimes.Count==0?0:frameTimes[(int)Math.Ceiling(frameTimes.Count*.95)-1],maxFrameMs=frameTimes.Count==0?0:frameTimes[frameTimes.Count-1]});
            frameTimes.Clear();report.samples=rows.ToArray();report.longFrames=gaps.ToArray();report.phaseMaxima=sustainedPhases?.maxima;
            File.WriteAllText(Path.Combine(directory,"sustained.json"),JsonUtility.ToJson(report,true));
            if(sustainedPhases!=null&&sustainedPhases.recording)
            {
                double saveMs=SustainedPhaseState.Milliseconds(saveAt);sustainedPhases.pendingSaveMs+=saveMs;
                sustainedPhases.pendingSaveGc0+=GC.CollectionCount(0)-gcAt;sustainedPhases.maxima.diagnosticSaveMs=Math.Max(sustainedPhases.maxima.diagnosticSaveMs,saveMs);
            }
        }
        try
        {
            Directory.CreateDirectory(directory);var args=Environment.GetCommandLineArgs();
            for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivSustainedSeconds")report.requestedSeconds=int.Parse(args[i+1]);
            report.phaseTimings=Array.IndexOf(args,"-livlivSustainedPhases")>=0;
            if(report.phaseTimings)sustainedPhases=new SustainedPhaseState();
            if(report.requestedSeconds<30||report.requestedSeconds>7200)throw new ArgumentOutOfRangeException("duration");
            await Task.Delay(1500);Save("baseline");
            source=new SmokeSource(Resources.Load<TextAsset>("LivLivPortrait").bytes);
            native=new NativeFaceInput(Resources.Load<TextAsset>("LivLivFaceModel").bytes,sustainedPhases==null?(INativeFaceSource)source:new TimedSource(source,sustainedPhases),30);
            faceInput=sustainedPhases==null?(IFaceInput)native:new TimedFace(native,sustainedPhases);
            face=FaceTracking.Attach(player,faceInput,NativeFaceInput.Now,new FaceTrackingOptions{follow=1});faceStatus="Active";
            voice=new SustainedVoice();microphoneInput=sustainedPhases==null?(IVoiceInput)voice:new TimedVoice(voice,sustainedPhases);
            lip=LipSync.Attach(player,microphoneInput);status="Generated PCM active";
            double startupDeadline=Time.realtimeSinceStartupAsDouble+20;
            while(!face.Face||player.Renderer.Core.@params.mouthOpen<.05)
            {if(Time.realtimeSinceStartupAsDouble>startupDeadline)throw new TimeoutException("Generated inputs did not activate");await Task.Yield();}
            await Task.Delay(5000);
            if(sustainedPhases!=null){sustainedPhases.recording=true;sustainedPhases.pendingSaveMs=0;sustainedPhases.pendingSaveGc0=0;}
            double activeStart=Time.realtimeSinceStartupAsDouble;Save("active");
            int previous=frames;double previousAt=Time.realtimeSinceStartupAsDouble,next=10;var baseline=rows[0].memory;var active=rows[1].memory;
            while(Time.realtimeSinceStartupAsDouble-activeStart<report.requestedSeconds)
            {
                await Task.Yield();if(frames==previous)continue;previous=frames;
                double observedAt=Time.realtimeSinceStartupAsDouble,gapMs=(observedAt-previousAt)*1000d;frameTimes.Add(gapMs);previousAt=observedAt;
                if(sustainedPhases!=null)
                {
                    if(sustainedPhases.last.frame!=frames)throw new Exception("Phase timings refer to a different frame");
                    if(gapMs>=50||sustainedPhases.last.updateMs>=50||sustainedPhases.pendingSaveMs>=50)
                    {if(gaps.Count>=10000)throw new Exception("Too many long frames");gaps.Add(new SustainedGap{elapsed=observedAt-began,wallGapMs=gapMs,timing=sustainedPhases.last,diagnosticSaveMs=sustainedPhases.pendingSaveMs,diagnosticGc0Completed=sustainedPhases.pendingSaveGc0});}
                    sustainedPhases.pendingSaveMs=0;sustainedPhases.pendingSaveGc0=0;
                }
                if(fatal||face==null||lip==null||!faceInput.Ready||player.Renderer.Core.move.s!=1)throw new Exception("Input/render lifecycle failed: "+error);
                if(Time.realtimeSinceStartupAsDouble-activeStart>=next)
                {
                    Save("active");next+=10;var m=rows[rows.Count-1].memory;
                    if(m.cameras!=baseline.cameras||m.clips!=baseline.clips||m.textures!=active.textures||m.targets!=active.targets||m.materials!=active.materials||m.meshes!=active.meshes)
                        throw new Exception("Generated input/render object counts changed during sustained run");
                }
            }
            Save("active-final");report.result="Passed";
        }
        catch(Exception exception){report.result="Failed";report.error=exception.ToString();Debug.LogException(exception);}
        finally
        {
            if(sustainedPhases!=null)sustainedPhases.recording=false;
            StopFace();StopMicrophone();native?.Dispose();source?.Dispose();voice?.Dispose();await Task.Delay(1000);
            report.sourceReleased=source?.disposed??true;report.voiceReleased=voice?.disposed??true;
            Save("stopped");report.samples=rows.ToArray();
            File.WriteAllText(Path.Combine(directory,"sustained.json"),JsonUtility.ToJson(report,true));
            sustainedPhases=null;
        }
    }
}
#endif
