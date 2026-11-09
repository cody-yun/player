#if !UNITY_WEBGL || UNITY_EDITOR
// Opt-in diagnostic: split the same renderer Update into physics/deform and render submission.
// Runs independently from lifecycle timing and does not instrument the distributed runtime.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivLiv;
using LivLiv.Platform;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class ModelProfileRow
    {
        public string model;public int frames,vertices,layers;
        public double physicsMeanMs,deformMeanMs,renderMeanMs,physicsP95Ms,deformP95Ms,renderP95Ms;
    }
    [Serializable]private sealed class ModelProfileReport
    {
        public string result="Running",error,platform,graphics,device,unity;
        public int width=512,height=768,warmup=10,frames=60;public ModelProfileRow[] rows;
    }
    private void StartModelProfileIfRequested()
    {
        var args=Environment.GetCommandLineArgs();string output=null,models=null;
        for(int i=0;i+1<args.Length;i++){if(args[i]=="-livlivModelProfile")output=args[i+1];if(args[i]=="-livlivSoakModels")models=args[i+1];}
        if(output!=null)StartCoroutine(ModelProfile(output,models));
    }
    private IEnumerator ModelProfile(string output,string models)
    {
        Directory.CreateDirectory(output);enabled=false;player.Pause();
        var report=new ModelProfileReport{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,unity=Application.unityVersion};
        var rows=new List<ModelProfileRow>();var run=RunModelProfile(report,rows,models,output);
        try
        {
            while(true)
            {
                bool more=false;try{more=run.MoveNext();}catch(Exception e){report.error=e.ToString();}
                if(!more)break;yield return run.Current;
            }
        }
        finally{(run as IDisposable)?.Dispose();}
        report.rows=rows.ToArray();report.result=report.error==null&&rows.Count==21?"Passed":"Failed";
        File.WriteAllText(Path.Combine(output,"model-profile.json"),JsonUtility.ToJson(report,true));
        Application.Quit(report.result=="Passed"?0:1);
    }
    private IEnumerator RunModelProfile(ModelProfileReport report,List<ModelProfileRow> rows,string models,string output)
    {
        var files=Directory.GetFiles(models,"*.livliv").OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        if(files.Length!=21)throw new Exception("Expected 21 source models");
        foreach(var file in files)
        {
            var target=new RenderTexture(report.width,report.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="LivLiv model profile"};
            LivLiv.Unity.Player current=null;
            try
            {
                if(!target.Create())throw new Exception("Profile target creation failed");
                current=new LivLiv.Unity.Player(target,new PlayerOptions{idle=false});current.Use(new SamplePresentation());
                current.Load(ModelPackage.FromZip(File.ReadAllBytes(file)));current.Tick(1d/60);
                var core=current.Renderer.Core;
                var row=new ModelProfileRow{model=Path.GetFileNameWithoutExtension(file),frames=report.frames,layers=core.layers.Count,vertices=core.layers.Sum(l=>l.output.Length/2)};
                var physics=new List<double>();var deform=new List<double>();var render=new List<double>();
                var clock=new System.Diagnostics.Stopwatch();
                for(int i=-report.warmup;i<report.frames;i++)
                {
                    double phase=i*Math.PI*2/30;
                    core.@params.angleX=8*Math.Sin(phase);core.@params.angleY=3*Math.Cos(phase);core.@params.mouthOpen=.5+.5*Math.Sin(phase);
                    clock.Restart();core.UpdatePhysics(1d/60);double a=clock.Elapsed.TotalMilliseconds;
                    clock.Restart();core.Deform();double b=clock.Elapsed.TotalMilliseconds;
                    clock.Restart();current.Renderer.Render(target,core.time*1000,1);double c=clock.Elapsed.TotalMilliseconds;
                    if(i>=0){physics.Add(a);deform.Add(b);render.Add(c);}
                    if(core.move.s!=1)throw new Exception("Sample scale changed");
                    yield return null;
                }
                row.physicsMeanMs=physics.Average();row.deformMeanMs=deform.Average();row.renderMeanMs=render.Average();
                row.physicsP95Ms=ModelP95(physics);row.deformP95Ms=ModelP95(deform);row.renderP95Ms=ModelP95(render);rows.Add(row);
                // SnapshotPng redraws with wall time, including the logo blink. Freeze only this
                // diagnostic render's watermark clock, then read raw premultiplied RGBA for parity.
                current.Renderer.Render(target,0,1);
                var previous=RenderTexture.active;Texture2D image=null;
                try
                {
                    RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
                    image.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);
                    File.WriteAllBytes(Path.Combine(output,row.model+".png"),image.EncodeToPNG());
                }
                finally{RenderTexture.active=previous;if(image!=null)Destroy(image);}
            }
            finally{current?.Dispose();target.Release();Destroy(target);}
            yield return null;yield return null;
        }
    }
}
#endif
