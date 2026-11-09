// QA only: request Unity orientations on the native player; never opens inputs.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class OrientationPhase
    {public string requested,screenshot,error,nativeError;public int androidRequested=-1,androidRotation=-1;public Evidence player;}
    [Serializable]private sealed class OrientationEvidence
    {public string result="Running",error,scope="Explicit Unity orientation requests; no sensor auto-rotation or physical-device claim";public bool autoRotationRestored;public List<OrientationPhase> phases=new List<OrientationPhase>();}
    private void StartOrientationProbeIfRequested()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivOrientationProbe"){StartCoroutine(OrientationProbe(args[i+1]));return;}
        for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivUiOrientation")
        {
            if(args[i+1]=="Portrait")Screen.orientation=ScreenOrientation.Portrait;
            else if(args[i+1]=="LandscapeLeft")Screen.orientation=ScreenOrientation.LandscapeLeft;
            else throw new ArgumentException("Unsupported QA UI orientation: "+args[i+1]);
            return;
        }
#endif
    }
    private OrientationPhase OrientationState(ScreenOrientation wanted,string file=null,string error=null)
    {
        var phase=new OrientationPhase{requested=wanted.ToString(),screenshot=file,error=error,player=ReadEvidence()};
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using(var unity=new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity=unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using(var manager=activity.Call<AndroidJavaObject>("getWindowManager"))
            using(var display=manager.Call<AndroidJavaObject>("getDefaultDisplay"))
            {phase.androidRequested=activity.Call<int>("getRequestedOrientation");phase.androidRotation=display.Call<int>("getRotation");}
        }
        catch(Exception diagnostic){phase.nativeError=diagnostic.ToString();}
#endif
        return phase;
    }
    private IEnumerator OrientationProbe(string directory)
    {
        var report=new OrientationEvidence();Directory.CreateDirectory(directory);
        try
        {
            foreach(var wanted in new[]{ScreenOrientation.Portrait,ScreenOrientation.LandscapeLeft,ScreenOrientation.PortraitUpsideDown,ScreenOrientation.LandscapeRight})
            {
                int before=frames;Screen.orientation=wanted;double deadline=Time.realtimeSinceStartupAsDouble+8;
                bool portrait=wanted==ScreenOrientation.Portrait||wanted==ScreenOrientation.PortraitUpsideDown;
                while((Screen.orientation!=wanted||(Screen.width<Screen.height)!=portrait||frames<=before+3)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                // Screen dimensions can change before the Android swapchain has stabilized.
                double stableUntil=Time.realtimeSinceStartupAsDouble+1.5;
                while(Time.realtimeSinceStartupAsDouble<stableUntil)yield return null;
                yield return new WaitForEndOfFrame();
                var marker=OrientationState(wanted);
                File.WriteAllText(Path.Combine(directory,wanted+".phase.json"),JsonUtility.ToJson(marker,true));
                // Host captures the actual Android display before Unity's GPU readback API runs.
                yield return new WaitForSecondsRealtime(2);
                yield return new WaitForEndOfFrame();
                Texture2D image=null;
                try
                {
                    var state=ReadEvidence();var layout=state.layout;
                    if(Screen.orientation!=wanted||(Screen.width<Screen.height)!=portrait||frames<=before+3)throw new Exception("Unity orientation did not settle: "+wanted);
                    if(state.status!="Ready"||state.scale!=1||state.tracking||state.microphone)throw new Exception("Player lost normal idle state");
                    if(layout==null||layout.visibleButtons!=6||layout.clippedButtons!=0||layout.overlappingPairs!=0||layout.scrollComponents!=0)throw new Exception("UGUI controls clipped, overlapped or missing");
                    string file=wanted+".png";image=ScreenCapture.CaptureScreenshotAsTexture();
                    if(image==null||image.width!=Screen.width||image.height!=Screen.height)throw new Exception("Screenshot size does not match Unity surface");
                    File.WriteAllBytes(Path.Combine(directory,file),image.EncodeToPNG());
                    report.phases.Add(OrientationState(wanted,file));
                }
                catch(Exception error)
                {
                    report.error=error.ToString();
                    report.phases.Add(OrientationState(wanted,error:error.ToString()));
                }
                finally{if(image!=null)Destroy(image);}
            }
            report.result=report.phases.Count==4&&report.error==null?"Passed":"Failed";
        }
        finally
        {
            // Verification sample's configured policy is auto rotation on all four sides.
            Screen.orientation=ScreenOrientation.AutoRotation;report.autoRotationRestored=true;
            File.WriteAllText(Path.Combine(directory,"orientation.json"),JsonUtility.ToJson(report,true));
            Application.Quit(report.result=="Passed"?0:1);
        }
    }
}
