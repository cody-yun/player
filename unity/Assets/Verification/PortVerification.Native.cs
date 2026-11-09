#if !UNITY_WEBGL || UNITY_EDITOR
// Automated standalone smoke uses an explicit command-line opt-in and generated video, never hardware.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LivLiv;
using LivLiv.Unity.MediaPipe;
using UnityEngine;
public sealed partial class PortVerification
{
    private string nativeSmokePath;
    private void StartNativeSmokeIfRequested()
    {
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivSmoke")nativeSmokePath=args[i+1];
        if(nativeSmokePath!=null){Directory.CreateDirectory(nativeSmokePath);StartCoroutine(NativeSmoke());}
        StartSpeechSmokeIfRequested();
        StartPresentationSmokeIfRequested();
        StartCalibrationSmokeIfRequested();
        StartTraceSmokeIfRequested();
        StartCaptureSoakIfRequested();
        StartModelSoakIfRequested();
        StartModelProfileIfRequested();
    }
    [Serializable]private sealed class NativeEvidence{public string platform,graphics,unity,result,error;public Evidence[] states;public bool released,sourceFailureHandled,restarted,expressionLabels;public int failedSourceReleases,restartedSourceReleases;}
    private IEnumerator NativeSmoke()
    {
        var states=new List<Evidence>();SmokeSource source=null;int stage=0;
        var report=new NativeEvidence{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),unity=Application.unityVersion};
        while(stage<10)
        {
            yield return new WaitForSecondsRealtime(1);
            bool failed=false;
            try
            {
                if(fatal)throw new Exception(error);
                if(stage==0)
                {
                    SaveNativeFrame("neutral");source=new SmokeSource(Resources.Load<TextAsset>("LivLivPortrait").bytes);
                    faceInput=new NativeFaceInput(Resources.Load<TextAsset>("LivLivFaceModel").bytes,source);
                    face=FaceTracking.Attach(player,faceInput,NativeFaceInput.Now,new FaceTrackingOptions{follow=1});faceStatus="Active";
                }
                if(stage==1){if(!face.Face)throw new Exception("Native smoke found no face: "+InputError());SaveNativeFrame("detected");face.Calibrate();source.angle=.25;}
                if(stage==2){if(!face.Face||Math.Abs(player.Renderer.Core.@params.angleZ)<5)throw new Exception("Native smoke rotation did not reach the player");SaveNativeFrame("rotated");source.blank=true;}
                if(stage==3){if(face.Face)throw new Exception("Native smoke did not lose blank face");source.blank=false;source.angle=0;}
                if(stage==4){if(!face.Face)throw new Exception("Native smoke did not reacquire face");source.zoom=.8;}
                if(stage==5){if(!face.Face)throw new Exception("Native smoke lost the farther face");SaveNativeFrame("farther");source.zoom=1.2;}
                if(stage==6){if(!face.Face)throw new Exception("Native smoke lost the nearer face");SaveNativeFrame("nearer");source.failure="Generated camera capture ended";}
                if(stage==7)
                {
                    report.sourceFailureHandled=face==null&&faceInput==null&&source.disposed&&faceStatus.StartsWith("Failed:")&&!player.IsDisposed;
                    report.failedSourceReleases=source.releases;
                    if(!report.sourceFailureHandled||report.failedSourceReleases!=1)throw new Exception("Native smoke source failure was not isolated and released");
                }
                if(stage==8)
                {
                    source=new SmokeSource(Resources.Load<TextAsset>("LivLivPortrait").bytes);
                    faceInput=new NativeFaceInput(Resources.Load<TextAsset>("LivLivFaceModel").bytes,source);
                    face=FaceTracking.Attach(player,faceInput,NativeFaceInput.Now,new FaceTrackingOptions{follow=1});faceStatus="Active";
                }
                if(stage==9)
                {
                    report.restarted=face.Face;if(!report.restarted)throw new Exception("Native smoke did not detect after failed source restart");
                    SaveNativeFrame("restarted");StopFace();report.released=source.disposed;report.restartedSourceReleases=source.releases;
                    if(!report.released||report.restartedSourceReleases!=1)throw new Exception("Native smoke restarted source not released");
                }
                var state=ReadEvidence();if(state.scale!=1)throw new Exception("Sample character scale changed: "+state.scale);states.Add(state);
                if(state.expressionCount!=4||string.IsNullOrEmpty(state.expressionLabel)||state.expressionTranslation!="Smile")throw new Exception("Typed expression labels were not preserved");
            }
            catch(Exception e){report.result="Failed";report.error=e.ToString();failed=true;}
            if(failed)break;stage++;
        }
        report.result=report.result??"Passed";report.expressionLabels=report.result=="Passed";report.states=states.ToArray();StopFace();
        File.WriteAllText(Path.Combine(nativeSmokePath,"native-player.json"),JsonUtility.ToJson(report,true));
        Application.Quit(report.result=="Passed"?0:1);
    }
    private void SaveNativeFrame(string name)
    {
        var previous=RenderTexture.active;var image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
        try{RenderTexture.active=target;image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(nativeSmokePath,name+".png"),image.EncodeToPNG());}
        finally{RenderTexture.active=previous;Destroy(image);}
    }
    private sealed class SmokeSource:INativeFaceSource,LivLiv.Platform.ICaptureStatus
    {
        public double angle,zoom=1;public bool blank,disposed;private readonly Color32[] original=new Color32[640*480],pixels=new Color32[640*480];private readonly Texture2D texture;
        public string failure;public int releases;public string Error=>failure;
        public SmokeSource(byte[] bytes)
        {
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try{if(!ImageConversion.LoadImage(image,bytes))throw new Exception("Portrait decode failed");var raw=image.GetPixels32();for(int y=0;y<480;y++)for(int x=0;x<640;x++)original[y*640+x]=raw[(image.height-1-y*615/480)*image.width+x*820/640];}
            finally{UnityEngine.Object.Destroy(image);}
            texture=new Texture2D(640,480,TextureFormat.RGBA32,false,true);
        }
        public bool Ready=>!disposed;
        public bool TryRead(out Texture2D value)
        {
            value=texture;if(disposed)return false;double c=Math.Cos(angle),s=Math.Sin(angle);
            for(int y=0;y<480;y++)for(int x=0;x<640;x++)
            {int sx=(int)Math.Round((c*(x-320)+s*(y-240))/zoom+320),sy=(int)Math.Round((-s*(x-320)+c*(y-240))/zoom+240);pixels[y*640+x]=!blank&&sx>=0&&sx<640&&sy>=0&&sy<480?original[sy*640+sx]:new Color32(0,0,0,255);}
            texture.SetPixels32(pixels);return true;
        }
        public void Dispose(){if(disposed)return;disposed=true;releases++;UnityEngine.Object.Destroy(texture);}
    }
}
#endif
