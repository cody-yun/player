#if !UNITY_WEBGL || UNITY_EDITOR
// CLI-only model lifecycle diagnostic. Assets are supplied separately, never bundled for distribution.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity;
using UnityEngine;
using UnityEngine.Profiling;
public sealed partial class PortVerification
{
    [Serializable]private sealed class ModelSoakRow
    {
        public string first,replacement;public int pass,width,height,frames,remainingObjects;
        public double loadMs,replaceMs,tickMeanMs,tickP95Ms,frameMeanMs,frameP95Ms;
        public long allocated,reserved,managedWithGarbage;
    }
    [Serializable]private sealed class ModelSoakReport
    {
        public string result="Running",error,platform,graphics,device,cpu,unity;
        public int passes=3,framesPerModel=30,modelCount;public double elapsedSeconds;
        public ModelSoakRow[] rows;public string[] retained;
    }
    private void StartModelSoakIfRequested()
    {
        var args=Environment.GetCommandLineArgs();string output=null,models=null;
        for(int i=0;i+1<args.Length;i++){if(args[i]=="-livlivModelSoak")output=args[i+1];if(args[i]=="-livlivSoakModels")models=args[i+1];}
        if(output!=null)StartCoroutine(ModelSoak(output,models));
    }
    private static Dictionary<int,string> ModelObjects()
    {
        var objects=new Dictionary<int,string>();
        foreach(var x in Resources.FindObjectsOfTypeAll<Texture2D>())if(x.name.StartsWith("LivLiv "))objects[x.GetInstanceID()]="texture:"+x.name;
        foreach(var x in Resources.FindObjectsOfTypeAll<RenderTexture>())if(x.name.StartsWith("LivLiv "))objects[x.GetInstanceID()]="target:"+x.name;
        foreach(var x in Resources.FindObjectsOfTypeAll<Mesh>())if(x.name.StartsWith("LivLiv "))objects[x.GetInstanceID()]="mesh:"+x.name;
        foreach(var x in Resources.FindObjectsOfTypeAll<Material>())if(x.shader!=null&&x.shader.name.StartsWith("LivLiv/"))objects[x.GetInstanceID()]="material:"+x.shader.name;
        return objects;
    }
    private static double ModelP95(List<double> values){var a=values.OrderBy(v=>v).ToArray();return a[(int)Math.Ceiling(a.Length*.95)-1];}
    private IEnumerator ModelSoak(string output,string models)
    {
        Directory.CreateDirectory(output);enabled=false;player.Pause();
        var report=new ModelSoakReport{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),
            device=SystemInfo.graphicsDeviceName,cpu=SystemInfo.processorType,unity=Application.unityVersion};
        var rows=new List<ModelSoakRow>();double started=Time.realtimeSinceStartupAsDouble;
        var run=RunModelSoak(report,rows,output,models);
        try
        {
            while(true)
            {
                bool more=false;try{more=run.MoveNext();}catch(Exception e){report.error=e.ToString();}
                if(!more)break;yield return run.Current;
            }
        }
        finally{(run as IDisposable)?.Dispose();}
        report.rows=rows.ToArray();report.elapsedSeconds=Time.realtimeSinceStartupAsDouble-started;
        report.result=report.error==null&&rows.Count==report.modelCount*report.passes?"Passed":"Failed";
        File.WriteAllText(Path.Combine(output,"model-soak.json"),JsonUtility.ToJson(report,true));
        Application.Quit(report.result=="Passed"?0:1);
    }
    private IEnumerator RunModelSoak(ModelSoakReport report,List<ModelSoakRow> rows,string output,string models)
    {
        if(string.IsNullOrEmpty(models))throw new Exception("Provide -livlivSoakModels with the test-only model directory");
        var files=Directory.GetFiles(models,"*.livliv").OrderBy(f=>f,StringComparer.Ordinal).ToArray();report.modelCount=files.Length;
        if(files.Length!=21)throw new Exception("Expected all 21 source models");
        yield return null;yield return null;var baseline=ModelObjects();
        for(int pass=0;pass<report.passes;pass++)for(int index=0;index<files.Length;index++)
        {
            int width=pass%2==0?512:320,height=pass%2==0?768:480;
            var row=new ModelSoakRow{first=Path.GetFileNameWithoutExtension(files[index]),replacement=Path.GetFileNameWithoutExtension(files[(index+1)%files.Length]),pass=pass,width=width,height=height,frames=report.framesPerModel};
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="LivLiv model soak"};
            LivLiv.Unity.Player p=null;
            var ticks=new List<double>();var intervals=new List<double>();
            try
            {
                if(!rt.Create())throw new Exception("Model soak target creation failed");
                p=new LivLiv.Unity.Player(rt,new PlayerOptions{idle=false});p.Use(new SamplePresentation());
                var clock=System.Diagnostics.Stopwatch.StartNew();p.Load(ModelPackage.FromZip(File.ReadAllBytes(files[index])));row.loadMs=clock.Elapsed.TotalMilliseconds;
                p.Tick(1d/60);yield return null;
                clock.Restart();p.Load(ModelPackage.FromZip(File.ReadAllBytes(files[(index+1)%files.Length])));row.replaceMs=clock.Elapsed.TotalMilliseconds;
                double prior=Time.realtimeSinceStartupAsDouble;
                for(int frame=0;frame<report.framesPerModel;frame++)
                {
                    double now=Time.realtimeSinceStartupAsDouble;if(frame>0)intervals.Add((now-prior)*1000);prior=now;
                    double phase=frame*Math.PI*2/report.framesPerModel;
                    p.@params.angleX=8*Math.Sin(phase);p.@params.angleY=3*Math.Cos(phase);p.@params.mouthOpen=.5+.5*Math.Sin(phase);
                    clock.Restart();p.Tick(1d/60);ticks.Add(clock.Elapsed.TotalMilliseconds);
                    if(p.Renderer.Core.move.s!=1)throw new Exception("Model soak sample scale changed");
                    yield return null;
                }
                row.tickMeanMs=ticks.Average();row.tickP95Ms=ModelP95(ticks);row.frameMeanMs=intervals.Average();row.frameP95Ms=ModelP95(intervals);
                if(pass==0)File.WriteAllBytes(Path.Combine(output,row.replacement+".png"),p.SnapshotPng());
            }
            finally{p?.Dispose();rt.Release();Destroy(rt);}
            yield return null;yield return null;
            var remaining=ModelObjects().Where(x=>!baseline.ContainsKey(x.Key)).ToArray();row.remainingObjects=remaining.Length;
            row.allocated=Profiler.GetTotalAllocatedMemoryLong();row.reserved=Profiler.GetTotalReservedMemoryLong();row.managedWithGarbage=Profiler.GetMonoUsedSizeLong();rows.Add(row);
            report.rows=rows.ToArray();
            if(remaining.Length>0){report.retained=remaining.Select(x=>x.Key+":"+x.Value).ToArray();throw new Exception("Model-owned render objects survived Dispose: "+row.replacement);}
            File.WriteAllText(Path.Combine(output,"model-soak.json"),JsonUtility.ToJson(report,true));
        }
    }
}
#endif
