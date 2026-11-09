// src/index.ts Player surface adapter. Unity owns the frame loop, display dimensions and render target.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace LivLiv.Unity
{
    public sealed class ModelInfo
    {
        public string name,format,watermark;public int version;public int[] size;
        public JObject meta;public ModelExpression[] expressions;
    }
    public sealed class ModelExpression { public string id,label;public JObject labels; }
    public sealed partial class Player : PlayerBase
    {
        public const string Version="0.3.0";
        /// <summary>Probe the current Unity graphics device on the main thread. Model codecs and capture devices are checked when used.</summary>
        public static bool IsSupported()
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return false;
            var shader=Resources.Load<Shader>("LivLivLayer");
            if(shader==null||!shader.isSupported)return false;
            RenderTexture probe=null;
            try
            {
                probe=new RenderTexture(8,8,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear)
                    {name="LivLiv support probe",hideFlags=HideFlags.HideAndDontSave};
                return probe.Create()&&probe.IsCreated()&&probe.depth>=24;
            }
            catch(Exception){return false;}
            finally{if(probe!=null){probe.Release();TextureDecoder.Destroy(probe);}}
        }
        public readonly PuppetRenderer Renderer;
        private readonly Action detachContextLoss;
        public ModelInfo Info { get; private set; }
        // Keep the model's concrete expression type, including all language labels (upstream issue #8).
        public ModelExpression[] Expressions=>Info?.expressions??Array.Empty<ModelExpression>();
        // Caller owns the target. It may be shared with a RawImage or a material on any platform.
        private RenderTexture target;
        private Action detachTargetRelease;
        public RenderTexture Target
        {
            get=>target;
            set
            {
                if(ReferenceEquals(target,value))return;
                Action detach=null;
                if(!disposed&&value!=null){RenderTargetRelease.Claim(value);detach=RenderTargetRelease.Observe(value,this);}
                detachTargetRelease?.Invoke();detachTargetRelease=detach;target=value;
            }
        }
        public double Width { get; set; }
        public double Height { get; set; }
        public double PixelRatio=>Target==null?1:Target.width/Math.Max(1,DisplayWidth);
        public Player(RenderTexture target,PlayerOptions options=null,IWebPDecoder webp=null,Func<double> random=null,IMarkTextRasterizer markText=null)
            :this(CreateClaimedRenderer(target,webp,markText),target,options,random){}
        private static PuppetRenderer CreateClaimedRenderer(RenderTexture target,IWebPDecoder webp,IMarkTextRasterizer markText)
        {
            // src/index.ts claims its canvas before acquiring any renderer resources.
            if(target!=null)RenderTargetRelease.Claim(target);
            return new PuppetRenderer(webp,markText);
        }
        private Player(PuppetRenderer renderer,RenderTexture target,PlayerOptions options,Func<double> random)
            :base(renderer.Core.@params,options,random)
        {
            Renderer=renderer;loads.OnCallbackError=Debug.LogException;
            try{Target=target;detachContextLoss=WebGLContextLoss.Subscribe(this);}catch{detachTargetRelease?.Invoke();renderer.Dispose();throw;}
        }
        protected override bool Drawable=>Renderer.Model!=null&&Target!=null;
        protected override bool HasContent=>Info!=null;
        protected override double Clock=>Renderer.Core.time;
        protected override IDictionary<string,double> ShownExpressions=>Renderer.Core.expr;
        protected override IEnumerable<string> ExpressionIds=>Info?.expressions.Select(e=>e.id)??Enumerable.Empty<string>();
        protected override Puppet Seen=>Renderer.Core;
        protected override double DisplayWidth=>Width>0?Width:Target?.width??1;
        protected override double DisplayHeight=>Height>0?Height:Target?.height??1;
        public ModelInfo Load(ModelPackage package)
        {
            if(disposed)throw new ObjectDisposedException(nameof(Player));
            if(ContextLost)throw new LivLivError("context-lost","LivLiv: the rendering context was lost");
            loads.Replace();ClearPoster();return Install(package);
        }
        // Invoke on Unity's main thread. Download is asynchronous; validation/decode/GPU commit runs there too.
        public Task<ModelInfo> LoadAsync(byte[] zip,LoadOptions options=null)=>StartLoad(options,job=>
        {
            job.Build();job.Cancellation.ThrowIfCancellationRequested();
            var package=ModelPackage.FromZip(zip,lang,job.Cancellation);job.Cancellation.ThrowIfCancellationRequested();return Task.FromResult(Install(package));
        });
        public Task<ModelInfo> LoadAsync(IReadOnlyDictionary<string,byte[]> files,LoadOptions options=null)=>StartLoad(options,job=>
        {
            job.Build();job.Cancellation.ThrowIfCancellationRequested();
            var package=ModelPackage.FromFiles(files,lang,job.Cancellation);job.Cancellation.ThrowIfCancellationRequested();return Task.FromResult(Install(package));
        });
        public Task<ModelInfo> LoadAsync(ModelPackage package,LoadOptions options=null)=>StartLoad(options,job=>
        {
            job.Build();job.Cancellation.ThrowIfCancellationRequested();return Task.FromResult(Install(package));
        });
        public Task<ModelInfo> LoadAsync(string url,LoadOptions options=null)=>StartLoad(options,async job=>
        {
            var package=await ModelDownload.Load(new Uri(url,UriKind.Absolute),job,new UnityModelTransport(),lang);
            job.Cancellation.ThrowIfCancellationRequested();return Install(package);
        });
        private ModelInfo Install(ModelPackage package)
        {
            if(disposed)throw new OperationCanceledException("LivLiv: the player was disposed");
            if(ContextLost)throw new LivLivError("context-lost","LivLiv: the rendering context was lost");
            Renderer.Load(package);var m=Renderer.Model;
            var expressions=m["expressions"] as JArray;
            if(expressions==null)expressions=((JArray)m["layers"]).Any(l=>(string)l["id"]=="expr_smile")?JArray.Parse("[{'id':'smile','label':'','labels':{'zh':'笑','zh-Hant':'笑','en':'Smile','ja':'笑顔'}}]"):new JArray();
            Info=new ModelInfo{name=(string)m["name"],format=(string)m["format"]??"livemaker-puppet",version=(int?)m["version"]??3,size=m["canvas"].ToObject<int[]>(),watermark=(string)m["watermark"],meta=m["meta"] as JObject,
                expressions=expressions.Select(e=>{string id=(string)e["id"],label=(string)e["label"]??"";var labels=e["labels"] as JObject??new JObject{[ModelFormat.LangOf(label)]=label};return new ModelExpression{id=id,label=ModelFormat.PickLabel(labels,lang,id),labels=labels};}).ToArray()};
            Neutral();if(!Playing)Resize();return Info;
        }
        public double[] CanvasToModel(double x,double y)=>Renderer.Core.CanvasToModel(x,y,DisplayWidth,DisplayHeight);
        public int Poke(double x,double y,double strength=1)=>Renderer.Core.Poke(x,y,strength);
        protected override void Show(double dt)
        {
            var p=Renderer.Core;p.view.x=view.x;p.view.y=view.y;p.view.s=view.zoom;p.focusHead=view.head;
            p.move.x=frame.move.x;p.move.y=frame.move.y+actions.y;p.move.s=frame.move.s;
            Renderer.Update(dt);Renderer.Render(Target,Time.realtimeSinceStartupAsDouble*1000,PixelRatio);
        }
        protected override void Free(){detachTargetRelease?.Invoke();detachTargetRelease=null;detachContextLoss?.Invoke();Renderer.Dispose();Info=null;}
    }
}
