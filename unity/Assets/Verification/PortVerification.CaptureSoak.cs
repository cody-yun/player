#if !UNITY_WEBGL || UNITY_EDITOR
// Explicit CLI-only live-input diagnostic. Does not alter ordinary sample timing or GC policy.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;

public sealed partial class PortVerification
{
    [Serializable] private sealed class CaptureMemory
    {
        public int cycle;public string stage;public double time;
        public long allocated,reserved,managedWithGarbage;
        public int cameras,clips,textures,targets,materials,meshes;
        public string[] targetNames,materialNames;
        public bool cameraActive,microphoneActive;public double scale;
    }
    [Serializable] private sealed class CaptureSoakReport
    {
        public string result="Running",error,platform,graphics;public int cycles=30;
        public int warmupCycles=5;
        public bool captureObjectsReturned,renderObjectsStableAfterWarmup;public CaptureMemory[] samples;
    }
    private void StartCaptureSoakIfRequested()
    {
        var args=Environment.GetCommandLineArgs();
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivSustained")
        {RunSustained(args[i+1]);return;}
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivInferenceSoak")
        {RunInferenceSoak(args[i+1]);return;}
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivCaptureEnd")
        {RunCaptureEnd(args[i+1]);return;}
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivCaptureLoss")
        {RunCaptureLoss(args[i+1]);return;}
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivCaptureSoak")
        {RunCaptureSoak(args[i+1]);break;}
    }
    [Serializable] private sealed class CaptureLossReport
    {
        public string result="Running",error,graphics;
        public bool activeBefore,lost,disposed,inputsStopped,restartRejected;
        public int callbacks,camerasBefore,camerasAfter,clipsBefore,clipsAfter;
    }
    private async void RunCaptureLoss(string directory)
    {
        var report=new CaptureLossReport{graphics=SystemInfo.graphicsDeviceType.ToString()};
        try
        {
            Directory.CreateDirectory(directory);await Task.Delay(1500);
            report.camerasBefore=Resources.FindObjectsOfTypeAll<WebCamTexture>().Length;
            report.clipsBefore=Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            StartFace();StartMicrophone();double deadline=Time.realtimeSinceStartupAsDouble+35;
            while(faceStarting!=null||starting!=null)
            {if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Capture startup");await Task.Yield();}
            await Task.Delay(2000);
            report.activeBefore=face!=null&&lip!=null&&faceInput.Ready;
            if(!report.activeBefore)throw new Exception("Capture did not activate: "+error);
            player.OnContextLost+=()=>report.callbacks++;
            // Native policy test uses an explicit notification, not a real driver reset.
            player.NotifyContextLost();player.NotifyContextLost();await Task.Delay(1000);
            report.lost=player.ContextLost;report.disposed=player.IsDisposed;
            report.inputsStopped=face==null&&lip==null&&faceInput==null&&starting==null&&faceStarting==null;
            StartFace();StartMicrophone();await Task.Delay(1000);
            report.restartRejected=face==null&&lip==null&&starting==null&&faceStarting==null;
            report.camerasAfter=Resources.FindObjectsOfTypeAll<WebCamTexture>().Length;
            report.clipsAfter=Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            if(!report.lost||!report.disposed||!report.inputsStopped||!report.restartRejected||report.callbacks!=1||
                report.camerasBefore!=report.camerasAfter||report.clipsBefore!=report.clipsAfter)
                throw new Exception("Capture loss cleanup invariant failed");
            report.result="Passed";
        }
        catch(Exception exception){report.result="Failed";report.error=exception.ToString();Debug.LogException(exception);}
        finally
        {
            StopFace();StopMicrophone();
            File.WriteAllText(Path.Combine(directory,"capture-loss.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LIVLIV_CAPTURE_LOSS "+report.result);
        }
    }
    private CaptureMemory CaptureMemoryAt(int cycle,string stage)=>new CaptureMemory
    {
        cycle=cycle,stage=stage,time=Time.realtimeSinceStartupAsDouble,
        allocated=Profiler.GetTotalAllocatedMemoryLong(),reserved=Profiler.GetTotalReservedMemoryLong(),
        // This includes uncollected garbage; only the final explicit GC sample is post-collection.
        managedWithGarbage=Profiler.GetMonoUsedSizeLong(),
        cameras=Resources.FindObjectsOfTypeAll<WebCamTexture>().Length,
        clips=Resources.FindObjectsOfTypeAll<AudioClip>().Length,
        textures=Resources.FindObjectsOfTypeAll<Texture2D>().Length,
        targets=Resources.FindObjectsOfTypeAll<RenderTexture>().Length,
        materials=Resources.FindObjectsOfTypeAll<Material>().Length,
        meshes=Resources.FindObjectsOfTypeAll<Mesh>().Length,
        targetNames=Resources.FindObjectsOfTypeAll<RenderTexture>().Select(o=>o.name+" #"+o.GetInstanceID()).OrderBy(s=>s).ToArray(),
        materialNames=Resources.FindObjectsOfTypeAll<Material>().Select(o=>o.name+" #"+o.GetInstanceID()).OrderBy(s=>s).ToArray(),
        cameraActive=face!=null,microphoneActive=lip!=null,scale=player.Renderer.Core.move.s
    };
    private async void RunCaptureSoak(string directory)
    {
        var report=new CaptureSoakReport{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString()};
        var rows=new List<CaptureMemory>();
        async Task Save(int cycle,string stage)
        {
            rows.Add(CaptureMemoryAt(cycle,stage));report.samples=rows.ToArray();
            File.WriteAllText(Path.Combine(directory,"capture-soak.json"),JsonUtility.ToJson(report,true));
            // Keep this capture state until the host records OS PSS/threads/AppOps.
            string ack=Path.Combine(directory,"ack-"+rows.Count);double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(!File.Exists(ack)){if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Host memory sample acknowledgment");await Task.Delay(50);}
            File.Delete(ack);
        }
        try
        {
            Directory.CreateDirectory(directory);await Task.Delay(1500);await Save(-1,"baseline");
            var baseline=rows[0];
            for(int cycle=0;cycle<report.cycles;cycle++)
            {
                StartFace();StartMicrophone();double deadline=Time.realtimeSinceStartupAsDouble+35;
                while(faceStarting!=null||starting!=null)
                {if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Capture startup");await Task.Yield();}
                if(face==null||lip==null)throw new Exception("Capture startup failed: "+faceStatus+" / "+status+" / "+error);
                await Task.Delay(2000);
                if(fatal||!faceInput.Ready||!string.IsNullOrEmpty(InputError()))throw new Exception("Capture input failed: "+error+" / "+InputError());
                await Save(cycle,"active");
                StopFace();StopMicrophone();await Task.Delay(1000);await Save(cycle,"stopped");
                var stopped=rows[rows.Count-1];
                if(stopped.cameraActive||stopped.microphoneActive||stopped.cameras!=baseline.cameras||stopped.clips!=baseline.clips||stopped.textures!=baseline.textures||stopped.scale!=1)
                    throw new Exception("Unity capture objects did not return to baseline at cycle "+cycle);
            }
            // Observe natural idle separately before an explicit diagnostic collection.
            await Task.Delay(10000);await Save(report.cycles,"idle-before-gc");
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(1000);
            await Save(report.cycles,"after-explicit-gc");report.captureObjectsReturned=true;
            var steady=rows.Where(row=>row.stage=="stopped"&&row.cycle>=report.warmupCycles).ToArray();
            report.renderObjectsStableAfterWarmup=steady.All(row=>row.targets==steady[0].targets&&row.materials==steady[0].materials&&row.meshes==steady[0].meshes);
            if(!report.renderObjectsStableAfterWarmup)throw new Exception("Render object counts changed after warmup; inspect named objects");
            report.result="Passed";
        }
        catch(Exception exception){report.result="Failed";report.error=exception.ToString();Debug.LogException(exception);}
        finally
        {
            StopFace();StopMicrophone();report.samples=rows.ToArray();
            File.WriteAllText(Path.Combine(directory,"capture-soak.json"),JsonUtility.ToJson(report,true));
            // Remain in the sample with inputs off so the harness can measure final OS PSS/AppOps.
            Debug.Log("LIVLIV_CAPTURE_SOAK "+report.result);
        }
    }
}
#endif
