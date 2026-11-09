#if !UNITY_WEBGL || UNITY_EDITOR
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class CaptureEndReport
    {
        public string result="Running",error,graphics;
        public bool activeBefore,cameraStopped,microphonePreserved,cameraRestarted,microphoneStopped,cameraPreserved,microphoneRestarted,renderContinued;
        public int camerasAfter,clipsAfter;
    }
    private async void RunCaptureEnd(string directory)
    {
        var report=new CaptureEndReport{graphics=SystemInfo.graphicsDeviceType.ToString()};
        async Task Started()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+35;
            while(faceStarting!=null||starting!=null)
            {if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("Capture startup");await Task.Yield();}
            if(face==null||lip==null||!faceInput.Ready)throw new Exception("Capture startup failed: "+faceStatus+" / "+status);
        }
        try
        {
            Directory.CreateDirectory(directory);await Task.Delay(1500);StartFace();StartMicrophone();await Started();await Task.Delay(1500);
            report.activeBefore=true;int previousFrames=frames;
            // Exercise Unity's capture-state boundary; this is not a physical device unplug.
            foreach(var camera in Resources.FindObjectsOfTypeAll<WebCamTexture>())camera.Stop();
            await Task.Delay(1000);report.cameraStopped=face==null&&faceInput==null&&faceStatus.StartsWith("Failed:");
            report.microphonePreserved=lip!=null&&Microphone.IsRecording(null);
            StartFace();await Started();report.cameraRestarted=true;
            Microphone.End(null);await Task.Delay(1000);
            report.microphoneStopped=lip==null&&microphoneInput==null&&status=="Microphone failed";
            report.cameraPreserved=face!=null&&faceInput.Ready;
            StartMicrophone();await Started();report.microphoneRestarted=true;
            report.renderContinued=!fatal&&frames>previousFrames&&player.Info!=null&&!player.IsDisposed;
            StopFace();StopMicrophone();await Task.Delay(1000);
            report.camerasAfter=Resources.FindObjectsOfTypeAll<WebCamTexture>().Length;
            report.clipsAfter=Resources.FindObjectsOfTypeAll<AudioClip>().Length;
            if(!report.cameraStopped||!report.microphonePreserved||!report.cameraRestarted||!report.microphoneStopped||
                !report.cameraPreserved||!report.microphoneRestarted||!report.renderContinued||report.camerasAfter!=0||report.clipsAfter!=0)
                throw new Exception("Native capture-end invariant failed");
            report.result="Passed";
        }
        catch(Exception exception){report.result="Failed";report.error=exception.ToString();Debug.LogException(exception);}
        finally
        {
            StopFace();StopMicrophone();File.WriteAllText(Path.Combine(directory,"capture-end.json"),JsonUtility.ToJson(report,true));
            Debug.Log("LIVLIV_CAPTURE_END "+report.result);
        }
    }
}
#endif
