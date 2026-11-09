#if !UNITY_WEBGL || UNITY_EDITOR
// Isolate MediaPipe task/image churn from camera and microphone capture. CLI-only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LivLiv.Unity.MediaPipe;
using UnityEngine;
public sealed partial class PortVerification
{
    private async void RunInferenceSoak(string directory)
    {
        var report=new CaptureSoakReport{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString()};
        var rows=new List<CaptureMemory>();NativeFaceInput input=null;
        async Task Save(int cycle,string stage)
        {
            rows.Add(CaptureMemoryAt(cycle,stage));report.samples=rows.ToArray();
            File.WriteAllText(Path.Combine(directory,"capture-soak.json"),JsonUtility.ToJson(report,true));
            string ack=Path.Combine(directory,"ack-"+rows.Count);double deadline=Time.realtimeSinceStartupAsDouble+20;
            while(!File.Exists(ack)){if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Host memory acknowledgment");await Task.Delay(50);}
            File.Delete(ack);
        }
        try
        {
            Directory.CreateDirectory(directory);await Task.Delay(1500);await Save(-1,"baseline");var baseline=rows[0];
            for(int cycle=0;cycle<report.cycles;cycle++)
            {
                var source=new SmokeSource(Resources.Load<TextAsset>("LivLivPortrait").bytes);
                input=new NativeFaceInput(Resources.Load<TextAsset>("LivLivFaceModel").bytes,source,30);
                double deadline=Time.realtimeSinceStartupAsDouble+2;bool found=false;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {found|=input.Next(NativeFaceInput.Now())?.m!=null;await Task.Yield();}
                if(!found||!string.IsNullOrEmpty(input.Error))throw new Exception("Generated inference failed: "+input.Error);
                await Save(cycle,"active");input.Dispose();input=null;await Task.Delay(1000);await Save(cycle,"stopped");
                var stopped=rows[rows.Count-1];
                if(stopped.cameras!=baseline.cameras||stopped.clips!=baseline.clips||stopped.textures!=baseline.textures||stopped.scale!=1)
                    throw new Exception("Inference source objects did not return to baseline");
            }
            await Task.Delay(10000);await Save(report.cycles,"idle-before-gc");
            GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();await Task.Delay(1000);await Save(report.cycles,"after-explicit-gc");
            report.captureObjectsReturned=true;
            var steady=rows.Where(row=>row.stage=="stopped"&&row.cycle>=report.warmupCycles).ToArray();
            report.renderObjectsStableAfterWarmup=steady.All(row=>row.targets==steady[0].targets&&row.materials==steady[0].materials&&row.meshes==steady[0].meshes);
            if(!report.renderObjectsStableAfterWarmup)throw new Exception("Render object counts changed after warmup");
            report.result="Passed";
        }
        catch(Exception exception){report.result="Failed";report.error=exception.ToString();Debug.LogException(exception);}
        finally
        {
            input?.Dispose();report.samples=rows.ToArray();
            File.WriteAllText(Path.Combine(directory,"capture-soak.json"),JsonUtility.ToJson(report,true));
        }
    }
}
#endif
