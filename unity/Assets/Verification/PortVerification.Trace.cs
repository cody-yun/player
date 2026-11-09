using System;
using System.IO;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;
public sealed partial class PortVerification
{
    private static JObject RunTraceProbe()
    {
        var rows=new JArray();
        foreach(bool narrow in new[]{false,true})
        {
            var replay=TraceFaceInput.Synthetic(narrow);var record=new RecordingFaceInput(replay);var tracker=new FaceTracker();var stats=new SynthStats(()=>tracker.mirror);var pose=new Params();int active=0,lost=0;
            using(record)for(int i=0;i<=1440;i++)
            {
                double now=1000+Math.Floor(i*1000d/30+.5);var f=record.Next(now);if(f!=null){if(f.m!=null||f.b!=null)active++;else lost++;}
                tracker.Poll(f,now);tracker.Apply(pose,1d/30);stats.Observe(replay.Source.At(now)/1000,pose);
            }
            string json=record.Recorder.Save();var decoded=TraceData.Parse(json);
            if(replay.Ready||record.Ready||decoded.Length!=1441||lost!=120||stats.Latest?.loops!=2)throw new Exception("Trace pipeline/lifecycle mismatch");
            rows.Add(new JObject{["narrow"]=narrow,["count"]=decoded.Length,["active"]=active,["lost"]=lost,["fps"]=replay.Fps,["stats"]=JObject.FromObject(stats.Latest),["first"]=decoded.First().t,["last"]=decoded.Last().t,["record"]=json});
        }
        return new JObject{["result"]="Passed",["platform"]=Application.platform.ToString(),["states"]=rows};
    }
#if UNITY_WEBGL && !UNITY_EDITOR
    public void TraceProbe()
    {
        JObject result;try{result=RunTraceProbe();}catch(Exception e){result=new JObject{["result"]="Failed",["error"]=e.ToString()};}
        LivLivProbe_Snapshot(result.ToString());
    }
#else
    private void StartTraceSmokeIfRequested()
    {
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivTraceSmoke")
        {
            string path=args[i+1];Directory.CreateDirectory(path);JObject result;
            try{result=RunTraceProbe();foreach(JObject row in result["states"]){File.WriteAllText(Path.Combine(path,(bool)row["narrow"]?"trace-narrow.json":"trace-normal.json"),(string)row["record"]);row.Remove("record");}}
            catch(Exception e){result=new JObject{["result"]="Failed",["error"]=e.ToString()};}
            File.WriteAllText(Path.Combine(path,"trace-player.json"),result.ToString());Application.Quit((string)result["result"]=="Passed"?0:1);return;
        }
    }
#endif
}
