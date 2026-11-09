using System.Runtime.InteropServices;

public sealed partial class PortVerification
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern int LivLivSampleVisibility_Bind(string objectName);
    [DllImport("__Internal")] private static extern void LivLivSampleVisibility_Unbind(int id);
    [DllImport("__Internal")] private static extern int LivLivSampleVisibility_Hidden();
    private int visibilityBinding;
#endif
    // Sample policy only: background capture is closed and requires an explicit restart.
    // Register a document event because WebGL's render loop/pause callbacks need not
    // run after a page becomes hidden. Ignore mere focus loss (permission dialogs).
    private void BindCaptureVisibility()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        visibilityBinding=LivLivSampleVisibility_Bind(gameObject.name);
#endif
    }
    private void UnbindCaptureVisibility()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if(visibilityBinding!=0)LivLivSampleVisibility_Unbind(visibilityBinding);
        visibilityBinding=0;
#endif
    }
    private bool CapturePageHidden()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return LivLivSampleVisibility_Hidden()!=0;
#else
        return false;
#endif
    }
    // Called synchronously by the sample's JS document lifecycle adapter.
    public void SuspendBrowserCapture(){StopMicrophone();StopFace();}
}
