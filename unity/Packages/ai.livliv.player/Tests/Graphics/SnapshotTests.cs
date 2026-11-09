using System;
using System.IO;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivLiv.Tests
{
    [Category("Graphics")]
    public sealed class SnapshotTests
    {
        private static RenderTexture Target(int w=16,int h=16)
        {
            var t=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            t.Create();return t;
        }
        private static void Free(RenderTexture t){if(t==null)return;t.Release();TextureDecoder.Destroy(t);}
        private static Texture2D Decode(byte[] bytes)
        {
            var t=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            Assert.That(t.LoadImage(bytes),Is.True);return t;
        }
        private static Color32[] Read(RenderTexture rt)
        {
            var previous=RenderTexture.active;var t=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false,true);
            try{RenderTexture.active=rt;t.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0,false);return t.GetPixels32();}
            finally{RenderTexture.active=previous;TextureDecoder.Destroy(t);}
        }
        [SetUp] public void RequireGraphics()=>Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
        [Test]
        public void StraightAlphaOrientationDataUrlAndCallerTargetSurvive()
        {
            var target=Target(2,2);var other=Target();var before=RenderTexture.active;Texture2D image=null;
            var input=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            var colors=new[]{new Color32(0,0,255,255),new Color32(5,7,9,0),new Color32(128,0,0,128),new Color32(0,64,0,128)};
            input.SetPixels32(colors);input.Apply();Graphics.Blit(input,target);
            try
            {
                using(var p=new Player(target,new PlayerOptions{autoplay=false,idle=false}))
                {
                    RenderTexture.active=other;
                    string url=p.Snapshot();Assert.That(url,Does.StartWith("data:image/png;base64,"));
                    image=Decode(Convert.FromBase64String(url.Substring(url.IndexOf(',')+1)));
                    Assert.That(RenderTexture.active,Is.SameAs(other));
                    Assert.That(image.GetPixels32(),Is.EqualTo(new[]{new Color32(0,0,255,255),new Color32(0,0,0,0),new Color32(255,0,0,128),new Color32(0,128,0,128)}));
                    var bytes=p.SnapshotPng();p.Dispose();
                    Assert.That(p.SnapshotPng(),Is.EqualTo(bytes),"Disposed source snapshot retains the canvas pixels");
                    Assert.That(target.IsCreated(),Is.True);
                }
            }
            finally{RenderTexture.active=before;TextureDecoder.Destroy(image);TextureDecoder.Destroy(input);Free(target);Free(other);}
        }
        private sealed class Observe : PlayerDriver
        {
            public int calls;public double dt;
            public override void Update(PlayerFrame frame,IPlayerHost p){calls++;dt=frame.dt;frame.@params.mouthOpen=.7;}
        }
        [TestCase(false),TestCase(true)]
        public void SnapshotRedrawsHiddenOrPausedPoseWithoutAdvancingAndIncludesMark(bool playing)
        {
            var target=Target(512,768);var other=Target();var before=RenderTexture.active;Texture2D image=null;
            try
            {
                using(var p=new Player(target,new PlayerOptions{autoplay=playing,idle=false}))
                {
                    p.Load(ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters/char06"))));
                    p.Resize();var prior=Read(target);p.@params.angleX=8;p.@params.angleY=-3;p.visible=false;
                    var driver=new Observe();p.Use(driver);double time=p.Renderer.Core.time;
                    int textures=Resources.FindObjectsOfTypeAll<Texture2D>().Length;
                    RenderTexture.active=other;var bytes=p.SnapshotPng();image=Decode(bytes);
                    Assert.That(RenderTexture.active,Is.SameAs(other));Assert.That(driver.calls,Is.EqualTo(1));Assert.That(driver.dt,Is.Zero);
                    Assert.That(p.Renderer.Core.time,Is.EqualTo(time));Assert.That(p.Playing,Is.EqualTo(playing));Assert.That(p.visible,Is.False);
                    Assert.That(p.Renderer.Core.@params.mouthOpen,Is.EqualTo(.7));
                    var raw=Read(target);var png=image.GetPixels32();
                    Assert.That(raw.SequenceEqual(prior),Is.False,"snapshot must redraw changed pose");
                    int active=0,partial=0,mark=0;
                    for(int i=0;i<raw.Length;i++)
                    {
                        Assert.That(png[i].a,Is.EqualTo(raw[i].a));if(raw[i].a==0)continue;
                        active++;if(raw[i].a<255)partial++;
                        // Compare in the compositor's premultiplied space: conversion roundtrip <= 1 LSB.
                        Assert.That(Math.Abs(png[i].r*png[i].a/255d-raw[i].r),Is.LessThanOrEqualTo(1));
                        Assert.That(Math.Abs(png[i].g*png[i].a/255d-raw[i].g),Is.LessThanOrEqualTo(1));
                        Assert.That(Math.Abs(png[i].b*png[i].a/255d-raw[i].b),Is.LessThanOrEqualTo(1));
                        if(i/target.width<80&&i%target.width>400)mark++;
                    }
                    Assert.That(active,Is.GreaterThan(10000));Assert.That(partial,Is.GreaterThan(100));Assert.That(mark,Is.GreaterThan(100));
                    TextureDecoder.Destroy(image);image=null;
                    for(int i=0;i<4;i++)p.SnapshotPng();
                    Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Length,Is.EqualTo(textures),"readback images must not leak");
                    File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults/snapshot-char06-"+playing+".png")),bytes);
                }
            }
            finally{RenderTexture.active=before;TextureDecoder.Destroy(image);Free(target);Free(other);}
        }
        [Test]
        public void InvalidTargetAndReadbackBudgetDoNotChangeActiveSurface()
        {
            var before=RenderTexture.active;var other=Target();var uncreated=new RenderTexture(16,16,24);var huge=new RenderTexture(8192,8192,24);
            try
            {
                using(var p=new Player(null))
                {
                    RenderTexture.active=other;Assert.Throws<InvalidOperationException>(()=>p.Snapshot());
                    p.Target=uncreated;Assert.Throws<InvalidOperationException>(()=>p.Snapshot());
                    p.Target=huge;Assert.That(Assert.Throws<LivLivError>(()=>p.Snapshot()).code,Is.EqualTo("too-large"));
                    Assert.That(RenderTexture.active,Is.SameAs(other));Assert.That(huge.IsCreated(),Is.False);
                }
            }
            finally{RenderTexture.active=before;Free(other);Free(uncreated);Free(huge);}
        }
    }
}
