// CLI-created verification scene. Device activation is explicit; no camera/microphone opens on load.
using System;
using System.Runtime.InteropServices;
using System.Threading;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity;
using UnityEngine;
public sealed partial class PortVerification : MonoBehaviour
{
    private LivLiv.Unity.Player player;private RenderTexture target;private LipSync lip;private CancellationTokenSource starting;
    private readonly CaptureActivationGate micActivation=new CaptureActivationGate(),faceActivation=new CaptureActivationGate();
    private FaceTracking face;private IFaceInput faceInput;private CancellationTokenSource faceStarting;private string faceStatus="Off";
    private IVoiceInput microphoneInput;
    private string status="Loading",error;private int frames;private bool animate,fatal;private double previewX,previewY;
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]private static extern void LivLivProbe_Report(string json);
    [DllImport("__Internal")]private static extern void LivLivProbe_Image(string base64);
#endif
    private void Start()
    {
        try
        {
            target=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            player=new LivLiv.Unity.Player(target,new PlayerOptions{idle=false},markText:new UnityMarkText(GetComponent<VerificationHud>()?.MarkFont));
            BindPlayerLoss();
            player.Use(new SamplePresentation());
            GetComponent<VerificationHud>()?.Bind(this,target,player);
            player.Load(ModelPackage.FromZip(Resources.Load<TextAsset>("LivLivVerification").bytes));
            BindCaptureVisibility();status="Ready";Report();
#if !UNITY_WEBGL || UNITY_EDITOR
            StartNativeSmokeIfRequested();
            StartReleaseProbeIfRequested();
            StartOrientationProbeIfRequested();
#endif
        }
        catch(Exception e){error=e.ToString();status="Failed";fatal=true;Debug.LogException(e);Report();}
    }
    // Sample policy, outside the 1:1 upstream renderer/tracking implementation.
    // X/Y are LivLiv parameter units, not physical degrees. Bound their combined motion too.
    private sealed class SamplePresentation : PlayerDriver
    {
        public override int Order=>int.MaxValue;
        public override void Update(PlayerFrame frame,IPlayerHost host)
        {
            frame.move.s=1;
            var p=frame.@params;double x=p.angleX/12,y=p.angleY/8,length=Math.Sqrt(x*x+y*y);
            if(length>1){p.angleX/=length;p.angleY/=length;}
        }
    }
    private static double EasePreview(double value,double target,double dt)
    {
        double result=value+(target-value)*(1-Math.Exp(-Math.Min(dt,.05)/.1));
        return Math.Abs(result-target)<.0001?target:result;
    }
    private void Update()
    {
        if(player==null||fatal)return;
        try
        {
            ProfileSustainedPhase("update-begin");
            CheckCaptureErrors();
            player.@params.angleX=EasePreview(player.@params.angleX,animate?8*Math.Sin(Time.unscaledTime*.8):previewX,Time.unscaledDeltaTime);
            player.@params.angleY=EasePreview(player.@params.angleY,animate?3*Math.Sin(Time.unscaledTime*1.3):previewY,Time.unscaledDeltaTime);
            ProfileSustainedPhase("tick-begin");player.Tick(Time.unscaledDeltaTime);ProfileSustainedPhase("tick-end");frames++;
            if(frames%15==0){ProfileSustainedPhase("report-begin");Report();ProfileSustainedPhase("report-end");}
            ProfileSustainedPhase("update-end");
#if UNITY_WEBGL && !UNITY_EDITOR
            UpdateSpeechProbe();
            UpdatePosterProbe();
            UpdateSurfaceProbe();
#endif
        }
        catch(Exception e){error=e.ToString();status="Failed";fatal=true;Debug.LogException(e);Report();}
    }
    // Optional native CLI diagnostic; calls are compiled out when no implementation exists (WebGL).
    partial void ProfileSustainedPhase(string phase);
    public async void StartMicrophone()
    {
        if(player==null||fatal||player.IsDisposed||player.ContextLost||starting!=null||lip!=null||CapturePageHidden())return;ResetPreview();var request=new CancellationTokenSource();starting=request;var token=request.Token;status="Microphone permission";error=null;Report();
        try{
#if !UNITY_WEBGL || UNITY_EDITOR
            await micActivation.AuthorizeAsync(t=>CapturePermissions.RequestAsync(false,t),token);
#endif
            var input=await MicrophoneInput.StartAsync(cancellationToken:token);if(token.IsCancellationRequested){input.Dispose();token.ThrowIfCancellationRequested();}lip=LipSync.Attach(player,input);microphoneInput=input;status="Microphone active";}
        catch(OperationCanceledException){status="Microphone stopped";}
        catch(Exception e){status="Microphone failed";error=e.Message;Debug.LogWarning(e.Message);}
        finally{if(ReferenceEquals(starting,request))starting=null;request.Dispose();Report();}
    }
    public void StopMicrophone(){starting?.Cancel();lip?.Stop();lip=null;microphoneInput=null;status="Microphone stopped";error=null;Report();}
    private void CheckCaptureErrors()
    {
        var cameraError=(faceInput as ICaptureStatus)?.Error;
        if(!string.IsNullOrEmpty(cameraError)){StopFace();faceStatus="Failed: "+cameraError;Report();}
        var microphoneError=(microphoneInput as ICaptureStatus)?.Error;
        if(!string.IsNullOrEmpty(microphoneError)){StopMicrophone();status="Microphone failed";error=microphoneError;Report();}
    }
    public async void StartFace()
    {
        if(player==null||fatal||player.IsDisposed||player.ContextLost||faceStarting!=null||face!=null||CapturePageHidden())return;ResetPreview();var request=new CancellationTokenSource();faceStarting=request;var token=request.Token;faceStatus="Starting";Report();
        try
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var input=await WebGLFaceInput.StartAsync(new BrowserFaceOptions{onProgress=p=>faceStatus=p.step+" "+Math.Round(p.progress*100)+"%"},token);
#else
            await faceActivation.AuthorizeAsync(t=>CapturePermissions.RequestAsync(true,t),token);
            var input=await LivLiv.Unity.MediaPipe.WebCamFaceSource.StartAsync(Resources.Load<TextAsset>("LivLivFaceModel").bytes,cancellationToken:token);
#endif
            if(token.IsCancellationRequested){input.Dispose();token.ThrowIfCancellationRequested();}
            faceInput=input;face=FaceTracking.Attach(player,input,WebGLFaceInput.Now,new FaceTrackingOptions{follow=1,remember="livliv-sample:char06",calibrationStore=UnityCalibrationStore.Instance});faceStatus="Active";
        }
        catch(OperationCanceledException){faceStatus="Stopped";}
        catch(Exception e){faceStatus="Failed: "+e.Message;Debug.LogWarning(e.Message);}
        finally{if(ReferenceEquals(faceStarting,request))faceStarting=null;request.Dispose();Report();}
    }
    public void StopFace(){faceStarting?.Cancel();face?.Stop();face=null;faceInput=null;faceStatus="Stopped";Report();}
    public void CalibrateFace(){face?.Calibrate();}
    public void SetPose(string name)
    {
        if(player==null)return;animate=name=="animate";previewX=name=="turn"?8:0;previewY=name=="turn"?-3:0;
        player.@params.eyeLOpen=player.@params.eyeROpen=name=="blink"?.2:1;player.@params.mouthOpen=name=="talk"?.8:0;player.Resize();Report();
    }
    private void ResetPreview(){SetPose("neutral");player.@params.angleX=player.@params.angleY=0;}
    public void CaptureFrame()
    {
        if(player==null)return;
        var core=player.Renderer.Core;core.time=0;core.ResetPhysics();core.ResetMouth();core.smileShown=0;
        player.Resize();player.Renderer.Render(target,0,1);
        var previous=RenderTexture.active;Texture2D image=null;
        try
        {
            RenderTexture.active=target;image=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
            image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
#if UNITY_WEBGL && !UNITY_EDITOR
            LivLivProbe_Image(Convert.ToBase64String(image.EncodeToPNG()));
#endif
        }
        finally{RenderTexture.active=previous;if(image!=null)Destroy(image);}
    }
    public struct HudState
    {
        public string model,cameraError,micError;
        public bool ready,fatal,cameraStarting,cameraActive,micStarting,micActive,faceSeen;
        public double mouth;
    }
    public HudState ReadHudState()=>new HudState
    {
        model=player?.Info?.name,ready=player!=null&&!fatal,fatal=fatal,
        cameraStarting=faceStarting!=null,cameraActive=face!=null,micStarting=starting!=null,micActive=lip!=null,
        faceSeen=face?.Face??false,mouth=player?.Renderer.Core?.@params.mouthOpen??0,
        cameraError=faceStatus.StartsWith("Failed:")?InputFailureHint(faceStatus,"카메라"):null,
        micError=status=="Microphone failed"?InputFailureHint(error,"마이크"):null
    };
    private static string InputFailureHint(string message,string device)
    {
        if(message!=null&&(message.Contains("permission")||message.Contains("NotAllowed")||message.Contains("denied")))
            return device+" 권한을 허용한 뒤 다시 시작해 주세요.";
        return device+" 연결을 확인하고 다시 시작해 주세요.";
    }
    public void ToggleCamera(){if(faceStarting!=null||face!=null)StopFace();else StartFace();}
    public void ToggleMicrophone(){if(starting!=null||lip!=null)StopMicrophone();else StartMicrophone();}
    [Serializable]private sealed class Evidence {public string status,error,model,expressionLabel,expressionTranslation;public int frames,expressionCount;public double mouth,vowel;public bool microphone,tracking,faceSeen,dark;public string faceStatus,faceError;public double faceFps,angleX,angleY,angleZ,scale;public VerificationHud.LayoutEvidence layout;}
    private Evidence ReadEvidence()
    {
        var expressions=player?.Expressions;var first=expressions!=null&&expressions.Length>0?expressions[0]:null;
        return new Evidence{layout=GetComponent<VerificationHud>()?.ReadLayoutEvidence(),status=status,error=error,model=player?.Info?.name,expressionCount=expressions?.Length??0,expressionLabel=first?.label,expressionTranslation=(string)first?.labels?["en"],frames=frames,mouth=player?.Renderer.Core?.@params.mouthOpen??0,vowel=player?.Renderer.Core?.@params.vowel??0,microphone=lip!=null,tracking=face!=null,faceSeen=face?.Face??false,dark=face?.Dark??false,faceStatus=faceStatus,faceError=InputError(),faceFps=face?.Fps??0,angleX=player?.Renderer.Core?.@params.angleX??0,angleY=player?.Renderer.Core?.@params.angleY??0,angleZ=player?.Renderer.Core?.@params.angleZ??0,scale=player?.Renderer.Core?.move.s??1};
    }
    private string InputError()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return (faceInput as WebGLFaceInput)?.Error;
#else
        return (faceInput as LivLiv.Unity.MediaPipe.NativeFaceInput)?.Error;
#endif
    }
    private void Report()
    {
        if(player!=null&&player.ContextLost){fatal=true;status="Context lost";error="Graphics context lost; restart the player";}
#if UNITY_WEBGL && !UNITY_EDITOR
        LivLivProbe_Report(JsonUtility.ToJson(ReadEvidence()));
#elif DEVELOPMENT_BUILD
        Debug.Log("LIVLIV_EVIDENCE "+JsonUtility.ToJson(ReadEvidence()));
#endif
    }
    private void BindPlayerLoss()=>player.OnContextLost=HandlePlayerLoss;
    private void HandlePlayerLoss()
    {
        fatal=true;UnbindCaptureVisibility();
        try{StopFace();}catch(Exception e){Debug.LogException(e);}
        try{StopMicrophone();}catch(Exception e){Debug.LogException(e);}
        try{player.Dispose();}finally{Report();}
    }
    private void OnApplicationPause(bool paused)
    {
        bool cancelMic=micActivation.SetPaused(paused),cancelFace=faceActivation.SetPaused(paused);
#if DEVELOPMENT_BUILD
        Debug.Log("LIVLIV_LIFECYCLE paused="+paused+" microphoneAuthorization="+micActivation.IsAuthorizing+" cameraAuthorization="+faceActivation.IsAuthorizing);
#endif
        if(!paused)return;
        // Keep only the permission request. Active devices always close on background.
        if(cancelMic)StopMicrophone();else {lip?.Stop();lip=null;microphoneInput=null;}
        if(cancelFace)StopFace();else {face?.Stop();face=null;faceInput=null;}
    }
    private void OnDestroy(){UnbindCaptureVisibility();faceStarting?.Cancel();face?.Stop();starting?.Cancel();lip?.Stop();player?.Dispose();if(target!=null){target.Release();Destroy(target);}}
}
