using System;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed partial class GraphicsTests
    {
        private static RenderTexture EyeTarget(PuppetRenderer renderer)=>(RenderTexture)typeof(PuppetRenderer).GetField("eyeMasks",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(renderer);
        [Test] public void CroppedMaskGrowsReusesRecoversAndReleases()
        {
            var files=Synthetic();files["textures/mask.png"]=Png(16,16,(x,y)=>new Color32(255,255,255,(byte)(x>=4&&x<12&&y>=4&&y<12?128:0)));
            RenderTexture target=null;var renderer=new PuppetRenderer();
            int before=Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="LivLiv eye masks");
            try
            {
                renderer.Load(ModelPackage.FromFiles(files));renderer.Core.layers.First(l=>(string)l.def["id"]=="iris").soft=1;
                void Render(int size)
                {
                    if(target!=null){target.Release();TextureDecoder.Destroy(target);}
                    target=new RenderTexture(size,size,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();renderer.Render(target);
                    var image=Read(target);try{Pixel(image,size*3/4,size/4,new Color32(64,0,128,192));}finally{TextureDecoder.Destroy(image);}
                }
                Render(256);var first=EyeTarget(renderer);Assert.That(first.width*first.height,Is.LessThan(256*256));
                Assert.That(first.width%64,Is.Zero);Assert.That(first.height%64,Is.Zero);
                renderer.Render(target);Assert.That(EyeTarget(renderer),Is.SameAs(first));
                Render(64);Assert.That(EyeTarget(renderer),Is.SameAs(first),"Storage must not shrink");
                Render(1024);var grown=EyeTarget(renderer);Assert.That(grown,Is.Not.SameAs(first));Assert.That(first==null,Is.True);
                Render(64);Assert.That(EyeTarget(renderer),Is.SameAs(grown));
                grown.Release();renderer.Render(target);Assert.That(EyeTarget(renderer).IsCreated(),Is.True);
                renderer.Load(ModelPackage.FromFiles(files));Assert.That(EyeTarget(renderer),Is.Null,"Replacement releases mask storage");
            }
            finally{renderer.Dispose();if(target!=null){target.Release();TextureDecoder.Destroy(target);}}
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="LivLiv eye masks"),Is.EqualTo(before));
        }
        [Test] public void OffscreenHiddenAndDisabledMasksDoNotAllocate()
        {
            var target=new RenderTexture(256,256,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(ModelPackage.FromFiles(Synthetic()));var iris=renderer.Core.layers.First(l=>(string)l.def["id"]=="iris");var eye=renderer.Core.layers.First(l=>(string)l.def["id"]=="eye");iris.soft=1;
                    renderer.Core.view.x=10;renderer.Render(target);Assert.That(eye.maskCh,Is.EqualTo(-1));Assert.That(EyeTarget(renderer),Is.Null);
                    renderer.Core.view.x=0;renderer.Core.hidden.Add("eye");renderer.Render(target);Assert.That(EyeTarget(renderer),Is.Null);
                    renderer.Core.hidden.Clear();renderer.SoftIris=false;renderer.Render(target);Assert.That(EyeTarget(renderer),Is.Null);
                    renderer.SoftIris=true;renderer.Render(target);Assert.That(eye.maskCh,Is.Zero);Assert.That(EyeTarget(renderer),Is.Not.Null);
                }
            }
            finally{target.Release();TextureDecoder.Destroy(target);}
        }
    }
}
