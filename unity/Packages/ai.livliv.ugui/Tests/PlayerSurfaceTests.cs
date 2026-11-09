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
    public sealed class PlayerSurfaceTests
    {
        private GameObject root,child;private PlayerSurface surface;private RawImage image;private RectTransform rect;
        private Texture2D original;
        [UnitySetUp] public IEnumerator Setup()
        {
            root=new GameObject("Surface test",typeof(RectTransform),typeof(Canvas));root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            child=new GameObject("Player",typeof(RectTransform),typeof(RawImage));child.transform.SetParent(root.transform,false);
            rect=(RectTransform)child.transform;rect.sizeDelta=new Vector2(160,240);image=child.GetComponent<RawImage>();
            original=new Texture2D(1,1);image.texture=original;
            surface=child.AddComponent<PlayerSurface>();surface.Initialize(new PlayerOptions{idle=false,autoplay=false});
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup(){UnityEngine.Object.Destroy(root);UnityEngine.Object.Destroy(original);yield return null;}
        [UnityTest] public IEnumerator ResizeReusesThenReplacesTargetAndKeepsCallerImage()
        {
            surface.Refresh();var first=surface.Target;Assert.That(first.width,Is.EqualTo(160));Assert.That(first.height,Is.EqualTo(240));
            surface.Refresh();Assert.That(surface.Target,Is.SameAs(first));
            rect.sizeDelta=new Vector2(240,160);surface.Refresh();var second=surface.Target;
            Assert.That(second,Is.Not.SameAs(first));Assert.That(second.width,Is.EqualTo(240));Assert.That(second.height,Is.EqualTo(160));
            Assert.That(surface.Player.Width,Is.EqualTo(240));Assert.That(image.texture,Is.SameAs(second));yield return null;
            Assert.That(first==null,Is.True);
            var p=surface.Player;var material=image.material;UnityEngine.Object.Destroy(surface);yield return null;
            Assert.That(p.IsDisposed,Is.True);Assert.That(second==null,Is.True);Assert.That(material==null,Is.True);Assert.That(image.texture,Is.SameAs(original));Assert.That(original!=null,Is.True);
        }
        [UnityTest] public IEnumerator PixelDensityCapAndBudgetFollowTransformScale()
        {
            rect.localScale=Vector3.one*3;surface.MaxPixelRatio=2;surface.Refresh();
            Assert.That(surface.Target.width,Is.EqualTo(320));Assert.That(surface.Target.height,Is.EqualTo(480));
            surface.MaxPixelRatio=1;surface.Refresh();Assert.That(surface.Target.width,Is.EqualTo(160));
            surface.MaxPixelRatio=2;surface.MaxPixels=10000;surface.Refresh();
            Assert.That(surface.ResolutionLimited,Is.True);Assert.That((long)surface.Target.width*surface.Target.height,Is.LessThanOrEqualTo(10000));yield return null;
        }
        [UnityTest] public IEnumerator ZeroAreaReleasesAndRecoveryAllocatesAgain()
        {
            var first=surface.Target;rect.sizeDelta=new Vector2(0,240);surface.Refresh();yield return null;
            Assert.That(surface.Target,Is.Null);Assert.That(surface.Player.Target,Is.Null);Assert.That(first==null,Is.True);
            rect.sizeDelta=new Vector2(160,240);surface.Refresh();Assert.That(surface.Target.IsCreated(),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator PausedResizeDrawsModelAndDisableRetainsPlaybackState()
        {
            var p=surface.Player;p.Load(ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters/char06"))));
            rect.sizeDelta=new Vector2(180,240);yield return null;yield return null;
            var previous=RenderTexture.active;var read=new Texture2D(180,240,TextureFormat.RGBA32,false,true);
            try
            {
                RenderTexture.active=surface.Target;read.ReadPixels(new Rect(0,0,180,240),0,0,false);
                int active=0;foreach(var c in read.GetPixels32())if(c.a>0)active++;Assert.That(active,Is.GreaterThan(1000));
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(read);}
            Assert.That(p.Playing,Is.False);Assert.That(p.Renderer.Core.time,Is.Zero);
            p.Play();yield return null;double start=p.Renderer.Core.time;Assert.That(start,Is.GreaterThan(0));
            surface.enabled=false;yield return null;yield return null;Assert.That(p.Renderer.Core.time,Is.EqualTo(start));Assert.That(p.Playing,Is.True);
            surface.enabled=true;yield return null;Assert.That(p.Renderer.Core.time,Is.GreaterThan(start));
        }
        [UnityTest] public IEnumerator SurfaceShaderPreservesPremultipliedAlphaAndClipContracts()
        {
            var input=new Texture2D(1,1,TextureFormat.RGBA32,false,true);input.SetPixel(0,0,new Color(128f/255,0,0,128f/255));input.Apply();
            var output=new RenderTexture(8,8,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);output.Create();
            var read=new Texture2D(8,8,TextureFormat.RGBA32,false,true);var material=new Material(image.material);var previous=RenderTexture.active;
            try
            {
                foreach(var mode in new[]{"normal","clip","stencil"})
                {
                    RenderTexture.active=output;GL.Clear(true,true,Color.blue);
                    if(mode=="clip"){material.EnableKeyword("UNITY_UI_CLIP_RECT");material.SetVector("_ClipRect",new Vector4(100,100,101,101));}
                    if(mode=="stencil"){material.DisableKeyword("UNITY_UI_CLIP_RECT");material.SetInt("_StencilComp",1);}
                    Graphics.Blit(input,output,material);RenderTexture.active=output;read.ReadPixels(new Rect(0,0,8,8),0,0,false);
                    var c=(Color32)read.GetPixel(4,4);
                    Assert.That(c.r,Is.EqualTo(mode=="normal"?128:0).Within(1),mode);Assert.That(c.b,Is.EqualTo(mode=="normal"?127:255).Within(1),mode);Assert.That(c.a,Is.EqualTo(255));
                }
            }
            finally{RenderTexture.active=previous;UnityEngine.Object.Destroy(input);UnityEngine.Object.Destroy(read);UnityEngine.Object.Destroy(material);output.Release();UnityEngine.Object.Destroy(output);}
            yield return null;
        }
        [UnityTest] public IEnumerator TargetLossIsRecoveredWithoutReplacingPlayer()
        {
            var p=surface.Player;var first=surface.Target;first.Release();surface.Refresh();
            Assert.That(surface.Player,Is.SameAs(p));Assert.That(surface.Target,Is.Not.SameAs(first));Assert.That(surface.Target.IsCreated(),Is.True);yield return null;
        }
    }
}
