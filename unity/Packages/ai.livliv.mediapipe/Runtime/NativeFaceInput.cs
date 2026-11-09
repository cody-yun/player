// Native boundary for src/runtime/tracking.ts. Numeric tracking remains in LivLiv.Runtime.
using System;
using LivLiv.Runtime;
using Mediapipe.Tasks.Core;
using Mediapipe.Tasks.Vision.Core;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using UnityEngine;
namespace LivLiv.Unity.MediaPipe
{
    // The texture must be readable RGBA32 with its first memory row at the upright image TOP.
    // The source owns texture/capture lifetime; this input owns the source after construction starts.
    public interface INativeFaceSource : IDisposable
    {
        bool Ready { get; }
        bool TryRead(out Texture2D topDownRgba);
    }
    public sealed class NativeFaceInput : IFaceInput,LivLiv.Platform.ICaptureStatus
    {
        private FaceLandmarker detector;private INativeFaceSource source;
        private FaceLandmarkerResult result=FaceLandmarkerResult.Alloc(1,true,true);
        private readonly double maxFps;private double detectAt,detectMs,countAt;private int counted;private long lastTimestamp=-1;
        private bool disposed;
        private string error;
        public bool Ready=>Error==null&&!disposed&&detector!=null&&source.Ready;
        public double Fps {get;private set;} public bool Dark {get;private set;}
        public string Error
        {
            get
            {
                if(!disposed&&source is LivLiv.Platform.ICaptureStatus status)
                {var failure=status.Error;if(!string.IsNullOrEmpty(failure)){error=failure;Dispose();}}
                return error;
            }
        }
        public static double Now()=>Time.realtimeSinceStartupAsDouble*1000;
        public NativeFaceInput(byte[] model,INativeFaceSource source,double maxFps=-1)
        {
            this.source=source??throw new ArgumentNullException(nameof(source));this.maxFps=maxFps;
            try
            {
                if(model==null||model.Length==0)throw new ArgumentException("A Face Landmarker task model is required",nameof(model));
                if(double.IsNaN(maxFps)||double.IsInfinity(maxFps)||maxFps<0&&maxFps!=-1)throw new ArgumentOutOfRangeException(nameof(maxFps));
                detector=FaceLandmarker.CreateFromOptions(new FaceLandmarkerOptions(new BaseOptions(BaseOptions.Delegate.CPU,modelAssetBuffer:model),
                    runningMode:RunningMode.VIDEO,numFaces:1,outputFaceBlendshapes:true,outputFaceTransformationMatrixes:true));
            }
            catch{Dispose();throw;}
        }
        public FaceFrame Next(double nowMilliseconds)
        {
            if(!Ready)return null;
            if(double.IsNaN(nowMilliseconds)||double.IsInfinity(nowMilliseconds)||nowMilliseconds<0||nowMilliseconds>=long.MaxValue)throw new ArgumentOutOfRangeException(nameof(nowMilliseconds));
            long timestamp=(long)nowMilliseconds;
            if(nowMilliseconds<detectAt||timestamp<=lastTimestamp)return null;
            try
            {
                if(!source.TryRead(out var texture))return null;
                if(texture==null||texture.format!=TextureFormat.RGBA32||!texture.isReadable)throw new ArgumentException("Face source must provide readable top-down RGBA32 pixels");
                double began=Now();lastTimestamp=timestamp;
                bool found;using(var image=new Mediapipe.Image(texture))found=detector.TryDetectForVideo(image,timestamp,null,ref result);
                if(maxFps!=0){double took=Math.Min(200,Now()-began);detectMs=detectMs!=0?detectMs+(took-detectMs)*.15:took/8;detectAt=nowMilliseconds+(maxFps==-1?Math.Min(200,2*detectMs):1000/maxFps);}
                var frame=found?ToFrame(result,nowMilliseconds):new FaceFrame{t=nowMilliseconds};if(frame.m!=null||frame.b!=null)counted++;
                if(nowMilliseconds-countAt>=1000)
                {
                    Fps=countAt!=0?Math.Round(counted*1000/(nowMilliseconds-countAt),MidpointRounding.AwayFromZero):0;counted=0;countAt=nowMilliseconds;
                    var pixels=texture.GetRawTextureData<Color32>();double sum=0;int samples=0;
                    for(int y=0;y<24;y++)for(int x=0;x<32;x++){var c=pixels[Math.Min(texture.height-1,y*texture.height/24)*texture.width+Math.Min(texture.width-1,x*texture.width/32)];sum+=.299*c.r+.587*c.g+.114*c.b;samples++;}
                    Dark=Dark?sum/samples<55:sum/samples<40;
                }
                return frame;
            }
            catch(Exception e){error=e.Message;Dispose();return null;}
        }
        public static FaceFrame ToFrame(FaceLandmarkerResult result,double timestamp)
        {
            var frame=new FaceFrame{t=timestamp};if(result.faceLandmarks==null||result.faceLandmarks.Count==0)return frame;
            if(result.facialTransformationMatrixes?.Count>0)
            {
                // Homuler 0.16.3 converts MediaPipe's matrix to Unity using S*M*S, S=diag(1,1,-1,1).
                // Undo that conversion, then retain column-major storage expected by original tracking.ts.
                frame.m=new double[16];var matrix=result.facialTransformationMatrixes[0];
                for(int c=0;c<4;c++)for(int r=0;r<4;r++)frame.m[c*4+r]=matrix[r,c]*((r==2?-1:1)*(c==2?-1:1));
            }
            if(result.faceBlendshapes?.Count>0&&result.faceBlendshapes[0].categories?.Count>0)
            {
                frame.b=new double[Trace.BS.Length];foreach(var category in result.faceBlendshapes[0].categories)
                {int i=Array.IndexOf(Trace.BS,category.categoryName);if(i>=0)frame.b[i]=category.score;}
            }
            var landmarks=result.faceLandmarks[0].landmarks;if(landmarks?.Count>152)
            {frame.l=new double[8];for(int i=0;i<Trace.LM.Length;i++){var p=landmarks[Trace.LM[i]];frame.l[2*i]=p.x;frame.l[2*i+1]=p.y;}}
            return frame;
        }
        public void Dispose(){if(disposed)return;disposed=true;try{((IDisposable)detector)?.Dispose();}finally{detector=null;source?.Dispose();source=null;}}
    }
}
