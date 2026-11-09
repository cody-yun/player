using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace LivLiv.Unity.MediaPipe
{
    public sealed class NativeFaceOptions
    {
        public string device;public bool preferFront=true;public int width=640,height=480,cameraFps=30;public double maxFps=-1;
    }
    public sealed class WebCamFaceSource : INativeFaceSource,LivLiv.Platform.ICaptureStatus
    {
        private static readonly HashSet<string> owned=new HashSet<string>(StringComparer.Ordinal);
        private readonly string device;private WebCamTexture camera;private Texture2D texture;private Color32[] raw,upright;private bool disposed;
        private bool activated;private string error;
        public string Error {get{if(!disposed&&activated&&(camera==null||!camera.isPlaying)){error="Camera capture ended; reconnect and start again";Dispose();}return error;}}
        private WebCamFaceSource(string device,NativeFaceOptions options){this.device=device;camera=new WebCamTexture(device,options.width,options.height,options.cameraFps);}
        public bool Ready=>!disposed&&camera!=null&&camera.isPlaying&&camera.width>16&&camera.height>16;
        // All calls and continuations belong on Unity main thread. Host stops/disposes on background/teardown.
        public static async Task<NativeFaceInput> StartAsync(byte[] model,NativeFaceOptions options=null,CancellationToken cancellationToken=default)
        {
            cancellationToken.ThrowIfCancellationRequested();options=options??new NativeFaceOptions();
            if(model==null||model.Length==0)throw new ArgumentException("A Face Landmarker task model is required",nameof(model));
            if(double.IsNaN(options.maxFps)||double.IsInfinity(options.maxFps)||options.maxFps<0&&options.maxFps!=-1)throw new ArgumentOutOfRangeException(nameof(options.maxFps));
            if(options.width<=0||options.height<=0||options.cameraFps<=0)throw new ArgumentOutOfRangeException(nameof(options));
            await CapturePermissions.RequestAsync(true,cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();var devices=WebCamTexture.devices;
            if(devices.Length==0)throw new LivLivError("invalid","LivLiv: no camera is available");
            string device=options.device;if(device==null){device=devices[0].name;foreach(var d in devices)if(d.isFrontFacing==options.preferFront){device=d.name;break;}}
            bool exists=false;foreach(var d in devices)exists|=d.name==device;if(!exists)throw new LivLivError("invalid","LivLiv: camera device was not found");
            if(!owned.Add(device))throw new LivLivError("invalid","LivLiv: this camera is already owned by LivLiv");
            WebCamFaceSource source=null;
            try
            {
                source=new WebCamFaceSource(device,options);source.camera.Play();double deadline=Time.realtimeSinceStartupAsDouble+15;
                while(!source.Ready){cancellationToken.ThrowIfCancellationRequested();if(Time.realtimeSinceStartupAsDouble>=deadline)throw new LivLivError("invalid","LivLiv: camera produced no readable frame");await Task.Yield();}
                cancellationToken.ThrowIfCancellationRequested();source.activated=true;var input=new NativeFaceInput(model,source,options.maxFps);
                if(cancellationToken.IsCancellationRequested){input.Dispose();cancellationToken.ThrowIfCancellationRequested();}return input;
            }
            catch{if(source!=null)source.Dispose();else owned.Remove(device);throw;}
        }
        public bool TryRead(out Texture2D topDownRgba)
        {
            topDownRgba=null;if(!Ready||!camera.didUpdateThisFrame)return false;
            int w=camera.width,h=camera.height,angle=camera.videoRotationAngle;
            raw=camera.GetPixels32(raw);int ow=angle%180==0?w:h,oh=angle%180==0?h:w;
            if(upright==null||upright.Length!=w*h)upright=new Color32[w*h];
            Orient(raw,w,h,camera.videoVerticallyMirrored,angle,upright);
            if(texture==null||texture.width!=ow||texture.height!=oh){DestroyTexture();texture=new Texture2D(ow,oh,TextureFormat.RGBA32,false,true);}
            texture.SetPixels32(upright); // CPU storage already top-down; no GPU upload needed for native Image.
            topDownRgba=texture;return true;
        }
        public static void Orient(Color32[] bottomUp,int width,int height,bool verticallyMirrored,int clockwiseDegrees,Color32[] topDown)
        {
            if(width<=0||height<=0||bottomUp==null||topDown==null||bottomUp.Length!=width*height||topDown.Length!=width*height||ReferenceEquals(bottomUp,topDown))throw new ArgumentException("Invalid camera pixel buffers");
            int a=(clockwiseDegrees%360+360)%360;if(a%90!=0)throw new ArgumentException("Camera rotation must be a multiple of 90 degrees");int ow=a%180==0?width:height;
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                int sx=x,sy=verticallyMirrored?y:height-1-y,dx=x,dy=y;
                if(a==90){dx=height-1-y;dy=x;}else if(a==180){dx=width-1-x;dy=height-1-y;}else if(a==270){dx=y;dy=width-1-x;}
                topDown[dy*ow+dx]=bottomUp[sy*width+sx];
            }
        }
        private void DestroyTexture(){if(texture==null)return;if(Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);texture=null;}
        public void Dispose(){if(disposed)return;disposed=true;try{if(camera!=null){camera.Stop();if(Application.isPlaying)UnityEngine.Object.Destroy(camera);else UnityEngine.Object.DestroyImmediate(camera);camera=null;}DestroyTexture();}finally{owned.Remove(device);}}
    }
}
