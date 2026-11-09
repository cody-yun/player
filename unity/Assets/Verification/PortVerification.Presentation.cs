#if !UNITY_WEBGL || UNITY_EDITOR
// Explicit native CLI probe: built-in model, file/data posters, no network or capture permissions.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Runtime;
using LivLiv.Unity;
using LivLiv.Unity.UI;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class PortVerification
{
    private void StartPresentationSmokeIfRequested()
    {
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivPresentationSmoke")
        {Directory.CreateDirectory(args[i+1]);StartCoroutine(PresentationSmoke(args[i+1]));return;}
    }
    [Serializable]private sealed class PresentationStep
    {public string name;public int width,height,targetId,active;public bool limited;public double time,scale,roundtrip;}
    [Serializable]private sealed class PresentationReport
    {
        public string result,error,platform,graphics,unity;public string[] startupLayout;
        public PresentationStep[] states;public NativeMaskStep[] masks;public TextMarkEvidence[] textMarks;public bool pngPoster,webpFilePoster,invalidPoster,latePoster,disposed;
        public CameraSurfaceStep[] cameraSurfaces;
        public string[] jpegPosters;
        public bool supported,contextLostHandled;public int supportProbes;
        public int targets,materials,posterTextures,maskTextures;
    }
    private sealed class PresentationHost:PlayerBase
    {
        public bool ready;private readonly Dictionary<string,double> shown=new Dictionary<string,double>();
        public PresentationHost():base(new Params(),new PlayerOptions{idle=false,autoplay=false}){}
        public Task<int> Load(string poster)=>StartLoad(new LoadOptions{poster=poster},j=>Task.FromResult(1));
        protected override bool Drawable=>ready;protected override double Clock=>0;
        protected override IDictionary<string,double> ShownExpressions=>shown;
        protected override IEnumerable<string> ExpressionIds=>Enumerable.Empty<string>();
        protected override Puppet Seen=>null;protected override double DisplayWidth=>100;protected override double DisplayHeight=>100;
        protected override void Show(double dt){}protected override void Free(){}
    }
    private sealed class PresentationTransport:IModelTransport
    {
        public readonly TaskCompletionSource<ModelResponse> result=new TaskCompletionSource<ModelResponse>();public CancellationToken token;
        public Task<ModelResponse> Get(Uri u,int cap,bool noCache,CancellationToken cancellation,Action<double> progress=null){token=cancellation;return result.Task;}
    }
    private static void RequirePresentation(bool condition,string why){if(!condition)throw new Exception(why);}
    private IEnumerator PresentationSmoke(string output)
    {
        var report=new PresentationReport{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),unity=Application.unityVersion};
        var run=RunPresentation(report,output);
        while(true)
        {
            bool more=false;try{more=run.MoveNext();}catch(Exception e){report.error=e.ToString();}
            if(!more)break;yield return run.Current;
        }
        (run as IDisposable)?.Dispose();yield return null;yield return null;
        foreach(var t in Resources.FindObjectsOfTypeAll<RenderTexture>())if(t.name=="LivLiv surface target")report.targets++;
        foreach(var t in Resources.FindObjectsOfTypeAll<RenderTexture>())if(t.name=="LivLiv support probe")report.supportProbes++;
        foreach(var m in Resources.FindObjectsOfTypeAll<Material>())if(m.name=="LivLiv surface material")report.materials++;
        foreach(var t in Resources.FindObjectsOfTypeAll<Texture2D>())if(t.name=="LivLiv poster")report.posterTextures++;
        foreach(var t in Resources.FindObjectsOfTypeAll<RenderTexture>())if(t.name=="LivLiv eye masks")report.maskTextures++;
        report.result=report.error==null&&report.disposed&&report.targets==0&&report.materials==0&&report.posterTextures==0&&report.maskTextures==0?"Passed":"Failed";
        File.WriteAllText(Path.Combine(output,"presentation-player.json"),JsonUtility.ToJson(report,true));Application.Quit(report.result=="Passed"?0:1);
    }
    private IEnumerator RunPresentation(PresentationReport report,string output)
    {
        var parent=GetComponent<VerificationHud>().Poster.transform.parent;
        var original=parent.Find("Character").GetComponent<RawImage>();original.enabled=false;
        var go=new GameObject("NativeSurfaceProbe",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
        var posterObject=new GameObject("NativePosterProbe",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));posterObject.transform.SetParent(parent,false);
        posterObject.GetComponent<RawImage>().enabled=false;
        var presenter=posterObject.AddComponent<PosterPresenter>();var host=new PresentationHost();presenter.Bind(host);
        var surface=go.AddComponent<PlayerSurface>();var p=surface.Initialize(new PlayerOptions{idle=false,autoplay=false});p.Use(new SamplePresentation());
        var package=ModelPackage.FromZip(Resources.Load<TextAsset>("LivLivVerification").bytes);p.Load(package);
        var states=new List<PresentationStep>();byte[] png=null;
        try
        {
            report.supported=LivLiv.Unity.Player.IsSupported();RequirePresentation(report.supported,"Player graphics preflight failed");
            // Native windows may report their final backing scale after the first Canvas update.
            // Observe it instead of treating a changed physical size as a same-size allocation bug.
            var startup=new List<string>();string last=null;int stable=0;double layoutDeadline=Time.realtimeSinceStartupAsDouble+5;
            ((RectTransform)go.transform).sizeDelta=new Vector2(320,480);
            while(stable<3&&Time.realtimeSinceStartupAsDouble<layoutDeadline)
            {
                Canvas.ForceUpdateCanvases();surface.Refresh();
                string stamp=Screen.width+"x"+Screen.height+"/"+surface.Target.width+"x"+surface.Target.height+"/"+go.transform.lossyScale.x.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
                startup.Add(stamp);stable=stamp==last?stable+1:0;last=stamp;yield return null;
            }
            report.startupLayout=startup.ToArray();RequirePresentation(stable>=3,"Canvas backing scale did not settle");
            int prior=0;
            var sizes=new[]{new[]{320,480,1,4194304},new[]{320,480,1,4194304},new[]{480,320,1,4194304},new[]{320,480,3,4194304},new[]{320,480,3,10000},new[]{0,480,1,4194304},new[]{320,480,1,4194304}};
            var names=new[]{"portrait","reuse","landscape","density","budget","zero","recovered"};
            for(int i=0;i<sizes.Length;i++)
            {
                var size=sizes[i];var rect=(RectTransform)go.transform;rect.sizeDelta=new Vector2(size[0],size[1]);rect.localScale=Vector3.one*size[2];surface.MaxPixels=size[3];surface.Refresh();yield return null;
                var t=surface.Target;var row=new PresentationStep{name=names[i],width=t!=null?t.width:0,height=t!=null?t.height:0,targetId=t!=null?t.GetInstanceID():0,limited=surface.ResolutionLimited,time=p.Renderer.Core.time,scale=p.Renderer.Core.move.s};
                RequirePresentation(row.time==0&&row.scale==1,"Pose time/scale changed");
                if(i==1)RequirePresentation(row.targetId==prior,"Same-sized target was replaced: previous="+prior+", current="+row.targetId+", pixels="+row.width+"x"+row.height);
                if(i==2)RequirePresentation(row.targetId!=prior,"Changed target was reused");
                if(i==5)RequirePresentation(t==null,"Zero target retained");
                if(t!=null)
                {
                    RequirePresentation((long)t.width*t.height<=size[3],"Pixel budget exceeded");
                    png=p.SnapshotPng();File.WriteAllBytes(Path.Combine(output,names[i]+".png"),png);
                    var decoded=new Texture2D(2,2,TextureFormat.RGBA32,false,true);var raw=new Texture2D(t.width,t.height,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
                    try
                    {
                        RequirePresentation(decoded.LoadImage(png)&&decoded.width==t.width&&decoded.height==t.height,"PNG decode/dimensions");
                        RenderTexture.active=t;raw.ReadPixels(new Rect(0,0,t.width,t.height),0,0,false);var a=decoded.GetPixels32();var b=raw.GetPixels32();
                        for(int n=0;n<a.Length;n++)
                        {
                            RequirePresentation(a[n].a==b[n].a,"Snapshot alpha changed");if(a[n].a>0)row.active++;
                            row.roundtrip=Math.Max(row.roundtrip,Math.Max(Math.Abs(a[n].r*a[n].a/255d-b[n].r),Math.Max(Math.Abs(a[n].g*a[n].a/255d-b[n].g),Math.Abs(a[n].b*a[n].a/255d-b[n].b))));
                        }
                        RequirePresentation(row.active>100&&row.roundtrip<=1,"Snapshot pixels/straight alpha");
                    }
                    finally{RenderTexture.active=previous;Destroy(decoded);Destroy(raw);}
                }
                if(i==4)RequirePresentation(row.limited,"Budget not marked limited");
                prior=row.targetId;states.Add(row);
            }
            report.states=states.ToArray();report.masks=RunNativeMasks(package,output);
            var cameraRows=new List<CameraSurfaceStep>();var cameraProbe=RunCameraSurfaces(package,output,cameraRows);
            try{while(cameraProbe.MoveNext())yield return cameraProbe.Current;}
            finally{(cameraProbe as IDisposable)?.Dispose();}
            report.cameraSurfaces=cameraRows.ToArray();
            var textMarks=new List<TextMarkEvidence>();
            foreach(string name in new[]{"empty","latin","korean","literal","default","retina-empty","retina","budget"})
            {
                TextMarkEvidence row=null;var textProbe=TextMarkProbe(name,r=>row=r);
                while(textProbe.MoveNext())yield return textProbe.Current;
                RequirePresentation(row!=null&&row.watermarks==0&&row.coverageTargets==0&&row.coverageMeshes==0&&row.coverageMaterials==0,"Text mark resources retained: "+name);
                if(name=="budget")RequirePresentation(row.code=="too-large","Text budget was not rejected");
                else
                {
                    RequirePresentation(string.IsNullOrEmpty(row.error)&&!string.IsNullOrEmpty(row.png),"Text mark failed: "+name+" / "+row.error);
                    File.WriteAllBytes(Path.Combine(output,"text-"+name+".png"),Convert.FromBase64String(row.png));
                }
                row.png=null;if(name=="budget")row.text="8193 UTF-16 units";textMarks.Add(row);
            }
            report.textMarks=textMarks.ToArray();string data="data:image/png;base64,"+Convert.ToBase64String(png);
            host.Load(data);yield return null;
            RequirePresentation(presenter.Showing&&presenter.LastError==null,"PNG poster failed");report.pngPoster=true;
            host.ready=true;host.Resize();yield return null;RequirePresentation(!presenter.Showing,"First frame did not clear poster");
            host.ready=false;string file=Path.Combine(output,"avatar.webp");byte[] webp=package.ReadImage(package.model,"avatar.webp");File.WriteAllBytes(file,webp);
            host.Load(new Uri(file).AbsoluteUri);double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(presenter.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            RequirePresentation(presenter.Showing&&presenter.LastError==null,"WebP file poster failed: "+presenter.LastError);report.webpFilePoster=true;
            var jpegPosters=new List<string>();
            foreach(string kind in new[]{"baseline","progressive"})
            {
                var jpeg=Resources.Load<TextAsset>("LivLivJpeg-"+kind).bytes;
                string url="data:image/jpeg;base64,"+Convert.ToBase64String(jpeg);
                if(kind=="progressive"){string path=Path.Combine(output,"poster-progressive.jpg");File.WriteAllBytes(path,jpeg);url=new Uri(path).AbsoluteUri;}
                host.Load(url);deadline=Time.realtimeSinceStartupAsDouble+10;
                while(presenter.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                RequirePresentation(presenter.Showing&&presenter.LastError==null,"JPEG poster failed: "+kind+" "+presenter.LastError);
                var texture=(Texture2D)posterObject.GetComponent<RawImage>().texture;
                RequirePresentation(texture.width==32&&texture.height==48,"JPEG dimensions changed");
                var pixels=texture.GetPixels32();var bottom=pixels[8*32+16];var top=pixels[40*32+16];
                RequirePresentation(bottom.b>245&&bottom.r<10&&top.r>245&&top.b<10&&bottom.a==255&&top.a==255,"JPEG colors/rows changed");
                File.WriteAllBytes(Path.Combine(output,"jpeg-"+kind+".png"),texture.EncodeToPNG());
                host.ready=true;host.Resize();yield return null;
                RequirePresentation(!presenter.Showing&&texture==null,"JPEG first-frame release failed");host.ready=false;jpegPosters.Add(kind);
            }
            report.jpegPosters=jpegPosters.ToArray();
            host.Load("data:image/png;base64,AAAA");yield return null;RequirePresentation(!presenter.Showing&&presenter.LastError!=null,"Invalid poster did not settle");report.invalidPoster=true;
            var transport=new PresentationTransport();presenter.Bind(host,null,transport);host.Load("https://example.test/held.webp");host.Load(data);yield return null;
            var current=posterObject.GetComponent<RawImage>().texture;RequirePresentation(transport.token.IsCancellationRequested,"Poster cancellation missing");
            transport.result.SetResult(new ModelResponse{bytes=webp});yield return null;
            RequirePresentation(presenter.Showing&&presenter.LastError==null&&posterObject.GetComponent<RawImage>().texture==current,"Late poster replaced current texture");report.latePoster=true;
            int lostCalls=0;p.OnContextLost=()=>lostCalls++;p.NotifyContextLost();p.NotifyContextLost();
            RequirePresentation(p.ContextLost&&lostCalls==1&&!p.IsDisposed,"Confirmed context loss lifecycle failed");
            try{p.Load((ModelPackage)null);throw new Exception("Lost player accepted sync load");}
            catch(LivLivError error){RequirePresentation(error.code=="context-lost","Wrong sync loss error");}
            var rejected=p.LoadAsync((ModelPackage)null);
            RequirePresentation(rejected.IsFaulted&&rejected.Exception.GetBaseException() is LivLivError lost&&lost.code=="context-lost","Wrong async loss error");
            p.Resize();RequirePresentation(p.Renderer.Core.time==0,"Lost player advanced pose");report.contextLostHandled=true;
        }
        finally
        {
            presenter.Unbind();host.Dispose();Destroy(posterObject);Destroy(go);original.enabled=true;
        }
        yield return null;report.disposed=p.IsDisposed;
    }
}
#endif
