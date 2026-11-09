using System;
using System.Collections;
using System.IO;
using LivLiv.Platform;
using LivLiv.Unity;
using LivLiv.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace LivLiv.Tests
{
    public sealed class PlayerSurfaceCameraTests
    {
        private GameObject root,cameraObject;
        private Canvas canvas;
        private Camera camera;
        private RenderTexture screen;
        private PlayerSurface surface;
        private RectTransform rect;
        [UnitySetUp] public IEnumerator Setup()
        {
            cameraObject=new GameObject("Surface camera",typeof(Camera));camera=cameraObject.GetComponent<Camera>();
            camera.enabled=false;camera.transform.position=new Vector3(0,0,-3);camera.fieldOfView=60;
            camera.nearClipPlane=.1f;camera.farClipPlane=100;camera.cullingMask=1<<30;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.blue;
            screen=new RenderTexture(640,480,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);screen.Create();camera.targetTexture=screen;
            root=new GameObject("World canvas",typeof(RectTransform),typeof(Canvas));root.layer=30;
            canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
            ((RectTransform)root.transform).sizeDelta=new Vector2(160,240);root.transform.localScale=Vector3.one*.01f;
            var child=new GameObject("Player",typeof(RectTransform),typeof(RawImage));child.layer=30;child.transform.SetParent(root.transform,false);
            rect=(RectTransform)child.transform;rect.sizeDelta=new Vector2(160,240);
            surface=child.AddComponent<PlayerSurface>();surface.Initialize(new PlayerOptions{idle=false,autoplay=false,maxPixelRatio=8});
            Canvas.ForceUpdateCanvases();yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            UnityEngine.Object.Destroy(root);camera.targetTexture=null;UnityEngine.Object.Destroy(cameraObject);
            screen.Release();UnityEngine.Object.Destroy(screen);yield return null;
        }
        [UnityTest] public IEnumerator PerspectiveUsesNearEdgeOnEitherSide()
        {
            // The two vertical edges have different depth. Both tilt directions
            // must resolve the near edge to the same pixel density.
            foreach(bool horizontal in new[]{true,false})
            {
                double nearDepth=3-(horizontal?.8:1.2)*Math.Sin(50*Math.PI/180);
                double density=480/(2*Math.Tan(Math.PI/6)*nearDepth)*.01;
                int width=(int)Math.Floor(160*density+.5),height=(int)Math.Floor(240*density+.5);
                foreach(float tilt in new[]{50f,-50f})
                {
                    root.transform.rotation=horizontal?Quaternion.Euler(0,tilt,0):Quaternion.Euler(tilt,0,0);surface.Refresh();
                    Assert.That(surface.Target.width,Is.EqualTo(width).Within(1),"horizontal="+horizontal+" tilt="+tilt);
                    Assert.That(surface.Target.height,Is.EqualTo(height).Within(1),"horizontal="+horizontal+" tilt="+tilt);
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CameraCanvasAndNestedCanvasUseAssignedCamera()
        {
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.planeDistance=3;
            Canvas.ForceUpdateCanvases();surface.Refresh();
            Assert.That(surface.Target.width,Is.EqualTo(160).Within(1));Assert.That(surface.Target.height,Is.EqualTo(240).Within(1));
            var nested=new GameObject("Nested canvas",typeof(RectTransform),typeof(Canvas));nested.layer=30;
            nested.transform.SetParent(root.transform,false);((RectTransform)nested.transform).sizeDelta=new Vector2(160,240);
            rect.SetParent(nested.transform,false);Canvas.ForceUpdateCanvases();surface.Refresh();
            Assert.That(surface.Target.width,Is.EqualTo(160).Within(1));Assert.That(surface.Target.height,Is.EqualTo(240).Within(1));
            yield return null;
        }
        [UnityTest] public IEnumerator PerspectiveCanvasActuallyCompositesCharacter()
        {
            surface.Player.Load(ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters/char06"))));
            root.transform.rotation=Quaternion.Euler(0,30,0);surface.Refresh();Canvas.ForceUpdateCanvases();yield return null;
            camera.Render();var previous=RenderTexture.active;var read=new Texture2D(640,480,TextureFormat.RGBA32,false,true);
            try
            {
                RenderTexture.active=screen;read.ReadPixels(new Rect(0,0,640,480),0,0,false);read.Apply();
                int changed=0;foreach(var c in read.GetPixels32())if(c.r>10||c.g>10)changed++;
                Assert.That(changed,Is.GreaterThan(10000),"World-space RawImage did not draw the character");
                var corner=(Color32)read.GetPixel(2,2);Assert.That(corner.r,Is.Zero);Assert.That(corner.b,Is.EqualTo(255));
                string path=Path.Combine(Application.dataPath,"../TestResults/surface-world-camera.png");File.WriteAllBytes(path,read.EncodeToPNG());
                Assert.That(surface.Player.Renderer.Core.time,Is.Zero,"Paused camera rendering must not advance animation");
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(read);}
        }
    }
}
