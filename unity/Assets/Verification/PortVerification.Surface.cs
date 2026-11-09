#if UNITY_WEBGL && !UNITY_EDITOR
// QA-only runtime exercise of the reusable UGUI component, within the existing stage.
using System;
using System.Runtime.InteropServices;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity.UI;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class PortVerification
{
    [DllImport("__Internal")]private static extern void LivLivProbe_Surface(string json,string png);
    [Serializable]private sealed class SurfaceRequest{public float width,height,scale=1,cap=2;public int pixels=4194304;}
    [Serializable]private sealed class SurfaceState
    {
        public int width,height,targetId,targets,materials;public float logicalWidth,logicalHeight,density;
        public double time,scale;public bool limited,playing;public string error;
    }
    private PlayerSurface surfaceProbe;private RawImage surfaceOriginal;private bool surfaceRecording;
    public void BeginSurfaceProbe()
    {
        EndSurfaceProbe();surfaceRecording=true;
        var parent=GetComponent<VerificationHud>().Poster.transform.parent;
        surfaceOriginal=parent.Find("Character").GetComponent<RawImage>();surfaceOriginal.enabled=false;
        var go=new GameObject("SurfaceProbe",typeof(RectTransform),typeof(RawImage));go.transform.SetParent(parent,false);
        go.GetComponent<RawImage>().raycastTarget=false;surfaceProbe=go.AddComponent<PlayerSurface>();
        var p=surfaceProbe.Initialize(new PlayerOptions{idle=false,autoplay=false});p.Use(new SamplePresentation());
        p.Load(ModelPackage.FromZip(Resources.Load<TextAsset>("LivLivVerification").bytes));
        ConfigureSurfaceProbe("{\"width\":320,\"height\":480,\"scale\":1,\"cap\":2,\"pixels\":4194304}");
    }
    public void ConfigureSurfaceProbe(string json)
    {
        try
        {
            var request=JsonUtility.FromJson<SurfaceRequest>(json);var rect=(RectTransform)surfaceProbe.transform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;
            rect.sizeDelta=new Vector2(request.width,request.height);rect.localScale=Vector3.one*request.scale;
            surfaceProbe.MaxPixelRatio=request.cap;surfaceProbe.MaxPixels=request.pixels;surfaceProbe.Refresh();
            ReportSurfaceProbe(surfaceProbe.Target!=null?surfaceProbe.Player.Snapshot():null);
        }
        catch(Exception e){LivLivProbe_Surface(JsonUtility.ToJson(new SurfaceState{error=e.ToString()}),null);}
    }
    public void EndSurfaceProbe()
    {
        if(surfaceProbe!=null){Destroy(surfaceProbe.gameObject);surfaceProbe=null;}
        if(surfaceOriginal!=null)surfaceOriginal.enabled=true;
    }
    private void UpdateSurfaceProbe(){if(surfaceRecording)ReportSurfaceProbe(null);}
    private void ReportSurfaceProbe(string png)
    {
        var result=new SurfaceState();
        foreach(var t in Resources.FindObjectsOfTypeAll<RenderTexture>())if(t.name=="LivLiv surface target")result.targets++;
        foreach(var m in Resources.FindObjectsOfTypeAll<Material>())if(m.name=="LivLiv surface material")result.materials++;
        if(surfaceProbe!=null)
        {
            var rect=(RectTransform)surfaceProbe.transform;var corners=new Vector3[4];rect.GetWorldCorners(corners);
            result.logicalWidth=rect.rect.width;result.logicalHeight=rect.rect.height;
            result.density=result.logicalWidth>0&&result.logicalHeight>0?Mathf.Max(Vector3.Distance(corners[0],corners[3])/result.logicalWidth,Vector3.Distance(corners[0],corners[1])/result.logicalHeight):1;
            var target=surfaceProbe.Target;result.width=target!=null?target.width:0;result.height=target!=null?target.height:0;result.targetId=target!=null?target.GetInstanceID():0;
            result.limited=surfaceProbe.ResolutionLimited;result.time=surfaceProbe.Player.Renderer.Core.time;result.scale=surfaceProbe.Player.Renderer.Core.move.s;result.playing=surfaceProbe.Player.Playing;
        }
        LivLivProbe_Surface(JsonUtility.ToJson(result),png);
    }
}
#endif
