#if !UNITY_WEBGL || UNITY_EDITOR
// Camera-space UGUI integration in the built player; explicit presentation probe only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity.UI;
using UnityEngine;
using UnityEngine.UI;
public sealed partial class PortVerification
{
    [Serializable]private sealed class CameraSurfaceStep
    {public string name;public int width,height,expectedWidth,expectedHeight,active;public double time,scale;}
    private IEnumerator RunCameraSurfaces(ModelPackage package,string output,List<CameraSurfaceStep> rows)
    {
        var cameraObject=new GameObject("Probe camera",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();
        var root=new GameObject("Probe world canvas",typeof(RectTransform),typeof(Canvas));root.layer=30;
        var screen=new RenderTexture(640,480,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
        var read=new Texture2D(640,480,TextureFormat.RGBA32,false,true);
        try
        {
            screen.Create();camera.enabled=false;camera.targetTexture=screen;camera.transform.position=new Vector3(0,0,-3);
            camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.blue;
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            ((RectTransform)root.transform).sizeDelta=new Vector2(160,240);root.transform.localScale=Vector3.one*.01f;
            var child=new GameObject("Probe camera player",typeof(RectTransform),typeof(RawImage));child.layer=30;child.transform.SetParent(root.transform,false);
            ((RectTransform)child.transform).sizeDelta=new Vector2(160,240);
            var surface=child.AddComponent<PlayerSurface>();var p=surface.Initialize(new PlayerOptions{idle=false,autoplay=false,maxPixelRatio=8});
            p.Use(new SamplePresentation());p.Load(package);
            foreach(string name in new[]{"front","right","left","up","down","orthographic","screen","nested"})
            {
                double depth=3;root.transform.rotation=Quaternion.identity;
                if(name=="right"||name=="left")
                {root.transform.rotation=Quaternion.Euler(0,name=="right"?50:-50,0);depth-=.8*Math.Sin(50*Math.PI/180);}
                if(name=="up"||name=="down")
                {root.transform.rotation=Quaternion.Euler(name=="up"?50:-50,0,0);depth-=1.2*Math.Sin(50*Math.PI/180);}
                double density=480/(2*Math.Tan(Math.PI/6)*depth)*.01;
                if(name=="orthographic"){camera.orthographic=true;camera.orthographicSize=2;density=1.2;}
                if(name=="screen"||name=="nested")
                {camera.orthographic=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.planeDistance=3;density=1;}
                if(name=="nested")
                {
                    var nested=new GameObject("Nested canvas",typeof(RectTransform),typeof(Canvas));nested.layer=30;nested.transform.SetParent(root.transform,false);
                    ((RectTransform)nested.transform).sizeDelta=new Vector2(160,240);child.transform.SetParent(nested.transform,false);
                }
                Canvas.ForceUpdateCanvases();yield return null;surface.Refresh();Canvas.ForceUpdateCanvases();camera.Render();
                var row=new CameraSurfaceStep{name=name,width=surface.Target.width,height=surface.Target.height,
                    expectedWidth=(int)Math.Floor(160*density+.5),expectedHeight=(int)Math.Floor(240*density+.5),time=p.Renderer.Core.time,scale=p.Renderer.Core.move.s};
                RequirePresentation(Math.Abs(row.width-row.expectedWidth)<=1&&Math.Abs(row.height-row.expectedHeight)<=1,"Camera target dimensions: "+name);
                var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=screen;read.ReadPixels(new Rect(0,0,640,480),0,0,false);read.Apply();
                    foreach(var pixel in read.GetPixels32())if(pixel.r>10||pixel.g>10)row.active++;
                    File.WriteAllBytes(Path.Combine(output,"camera-"+name+".png"),read.EncodeToPNG());
                }
                finally{RenderTexture.active=previous;}
                RequirePresentation(row.active>2000&&row.time==0&&row.scale==1,"Camera pixels/time/scale: "+name);rows.Add(row);
            }
        }
        finally
        {
            camera.targetTexture=null;Destroy(root);Destroy(cameraObject);screen.Release();Destroy(screen);Destroy(read);
        }
    }
}
#endif
