using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;
namespace LivLiv.Unity
{
    public sealed class BrowserFaceOptions
    {
        public string moduleUrl,wasmUrl,modelUrl;
        // -1 = upstream 'auto'; 0 = every new video frame; positive = inference FPS cap.
        public double maxFps=-1;
        public Action<BrowserFaceProgress> onProgress;
    }
    public sealed class BrowserFaceProgress { public string step;public double progress; }
    public sealed class WebGLFaceInput : IFaceInput,LivLiv.Platform.ICaptureStatus
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]private static extern int LivLivFace_Start(string json,int preload);
        [DllImport("__Internal")]private static extern int LivLivFace_State(int id);
        [DllImport("__Internal")]private static extern void LivLivFace_Info(int id,[Out]byte[] info,int capacity);
        [DllImport("__Internal")]private static extern double LivLivFace_Now();
        [DllImport("__Internal")]private static extern int LivLivFace_Poll(int id,double now,[Out]double[] data);
        [DllImport("__Internal")]private static extern void LivLivFace_Stop(int id);
#endif
        private int id;private bool disposed;private readonly byte[] infoBuffer=new byte[4096];private readonly double[] data=new double[42];
        private JObject info=new JObject();
#if UNITY_WEBGL && !UNITY_EDITOR
        private double infoAt=-1;
#endif
        private WebGLFaceInput(int id){this.id=id;}
        private void Refresh(bool force=false)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(disposed)return;double now=Now();if(!force&&now-infoAt<100)return;infoAt=now;LivLivFace_Info(id,infoBuffer,infoBuffer.Length);int n=Array.IndexOf(infoBuffer,(byte)0);info=JObject.Parse(Encoding.UTF8.GetString(infoBuffer,0,n<0?infoBuffer.Length:n));
#endif
        }
        public bool Ready
        {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return !disposed&&LivLivFace_State(id)==1;
#else
                return false;
#endif
            }
        }
        public double Fps {get{Refresh();return (double?)info["fps"]??0;}}
        public bool Dark {get{Refresh();return (bool?)info["dark"]??false;}}
        public string Error {get{Refresh(true);return (string)info["error"];}}
        public static double Now()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return LivLivFace_Now();
#else
            return Time.realtimeSinceStartupAsDouble*1000;
#endif
        }
        private static string Config(BrowserFaceOptions o)
        {
            string root=Application.streamingAssetsPath.TrimEnd('/')+"/LivLivFace";
            if(double.IsNaN(o.maxFps)||double.IsInfinity(o.maxFps)||o.maxFps<0&&o.maxFps!=-1)throw new ArgumentOutOfRangeException(nameof(o.maxFps));
            // StreamingAssets keep stable names while Unity's generated WASM is fingerprinted.
            // Give each player build its own module URL so a browser cannot mix new C# with cached old JS.
            return new JObject{["moduleUrl"]=o.moduleUrl??root+"/face-input.mjs?build="+Application.buildGUID,["wasmUrl"]=o.wasmUrl??root+"/wasm",["modelUrl"]=o.modelUrl??root+"/face_landmarker.task",["maxFps"]=o.maxFps==-1?(JToken)new JValue("auto"):new JValue(o.maxFps)}.ToString();
        }
        public static Task<WebGLFaceInput> StartAsync(BrowserFaceOptions options=null,CancellationToken cancellationToken=default)=>Open(options,false,cancellationToken);
        public static async Task PreloadAsync(BrowserFaceOptions options=null,CancellationToken cancellationToken=default){using(var input=await Open(options,true,cancellationToken)){} }
        private static async Task<WebGLFaceInput> Open(BrowserFaceOptions options,bool preload,CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            token.ThrowIfCancellationRequested();options=options??new BrowserFaceOptions();var input=new WebGLFaceInput(LivLivFace_Start(Config(options),preload?1:0));
            // Stop pending browser work synchronously; hidden pages may not run another frame.
            using var cancellation=token.Register(input.Dispose);
            try
            {
                while(LivLivFace_State(input.id)==0){token.ThrowIfCancellationRequested();input.Refresh();options.onProgress?.Invoke(new BrowserFaceProgress{step=(string)input.info["step"]??"download",progress=(double?)input.info["progress"]??0});await Task.Yield();}
                token.ThrowIfCancellationRequested();if(LivLivFace_State(input.id)<0){string error=input.Error??"Face input failed";if(error.StartsWith("NotAllowedError:",StringComparison.Ordinal)||error.StartsWith("SecurityError:",StringComparison.Ordinal))throw new UnauthorizedAccessException("LivLiv: "+error);throw new LivLivError("missing-feature","LivLiv: "+error);}
                return input;
            }
            catch{input.Dispose();throw;}
#else
            await Task.CompletedTask;throw new LivLivError("missing-feature","LivLiv: browser face input is available only in WebGL players");
#endif
        }
        public FaceFrame Next(double nowMilliseconds)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(disposed)return null;int flags=LivLivFace_Poll(id,nowMilliseconds,data);if(flags==0)return null;
            double[] Slice(int offset,int count){var copy=new double[count];Array.Copy(data,offset,copy,0,count);return copy;}
            return new FaceFrame{t=nowMilliseconds,m=(flags&2)!=0?Slice(0,16):null,b=(flags&4)!=0?Slice(16,18):null,l=(flags&8)!=0?Slice(34,8):null};
#else
            return null;
#endif
        }
        public void Dispose(){if(disposed)return;disposed=true;
#if UNITY_WEBGL && !UNITY_EDITOR
            LivLivFace_Stop(id);
#endif
        }
    }
}
