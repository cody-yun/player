using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Runtime;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed partial class GraphicsTests
    {
        [TestCase(5,24,"bottom")][TestCase(75,24,"bottom")]
        [TestCase(40,-4,"top")][TestCase(40,68,"bottom")]
        public void TextShadowAtEdgesMatchesLargerCanvasCrop(int x,int y,string baseline)
        {
            var command=new BrandDraw{kind="text",text="MW",x=x,y=y,fontSize=24,textBaseline=baseline,color="#fff",shadowColor="rgba(0,0,0,.6)",blur=8};
            var provider=new UnityMarkText();var actual=BrandRasterizer.Draw(64,64,new[]{command},provider);
            command.x+=32;command.y+=32;var expanded=BrandRasterizer.Draw(128,128,new[]{command},provider);
            int maxDifference=0,changed=0;
            for(int yy=0;yy<64;yy++)for(int xx=0;xx<64;xx++)for(int c=0;c<4;c++)
            {int delta=Math.Abs(actual[(yy*64+xx)*4+c]-expanded[((yy+32)*128+xx+32)*4+c]);maxDifference=Math.Max(maxDifference,delta);if(delta>2)changed++;}
            Assert.That(maxDifference,Is.LessThanOrEqualTo(2),"Allow at most two coverage levels for GPU projection rounding; channels exceeding tolerance: "+changed);
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="LivLiv text coverage"),Is.Zero);
        }
        [TestCase("Owner AV gy",false),TestCase("소유자 이름",true)]
        public void TextMarkRendersAtSourceCornerWithBorrowedFont(string text,bool korean)
        {
            Font font=korean?AssetDatabase.LoadAssetAtPath<Font>("Assets/Verification/Fonts/NanumGothic-Regular.ttf"):null;
            var provider=new UnityMarkText(font);var mark=new Mark();mark.Set("text",text);
            var commands=mark.Draw(512,768,0);var coverage=provider.Rasterize(512,768,commands[0]);
            Assert.That(coverage.alpha.Count(a=>a>.5),Is.GreaterThan(50));
            Assert.That(coverage.left+coverage.width,Is.LessThanOrEqualTo(505),"Right edge must include padding without clipping text");Assert.That(coverage.left,Is.GreaterThan(200));Assert.That(coverage.top,Is.GreaterThan(700));
            var bytes=BrandRasterizer.Draw(512,768,commands,provider);var image=new Texture2D(512,768,TextureFormat.RGBA32,false,true);
            try
            {
                // PNG uses bottom-up pixels; portable raster output is top-down.
                var colors=new Color32[512*768];for(int y=0;y<768;y++)for(int x=0;x<512;x++){int i=(y*512+x)*4;colors[(767-y)*512+x]=new Color32(bytes[i],bytes[i+1],bytes[i+2],bytes[i+3]);}
                image.SetPixels32(colors);image.Apply();File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults/text-mark-"+(korean?"korean":"latin")+".png")),image.EncodeToPNG());
                Assert.That(colors.Any(c=>c.a>c.r&&c.a>0),Is.True,"Black shadow must be composited");Assert.That(colors.Max(c=>c.r),Is.GreaterThan(180));
            }
            finally{TextureDecoder.Destroy(image);}
            if(korean)Assert.That(font!=null,Is.True,"Borrowed font is not destroyed");
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="LivLiv text coverage"),Is.Zero);
            Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(t=>t.name=="LivLiv text coverage"),Is.Zero);
        }
        [Test]public void TextMarkIsLiteralSingleLineAndMissingGlyphIsExplicit()
        {
            var r=new UnityMarkText();var command=new BrandDraw{kind="text",text="A\nB",x=300,y=150,fontSize=24,textBaseline="bottom",blur=6};
            var line=r.Rasterize(512,256,command);command.text="A B";var space=r.Rasterize(512,256,command);
            Assert.That(line.width,Is.EqualTo(space.width));Assert.That(line.alpha,Is.EqualTo(space.alpha));
            command.text="<b>A</b>";var literal=r.Rasterize(512,256,command);command.text="A";Assert.That(literal.width,Is.GreaterThan(r.Rasterize(512,256,command).width));
            command.text="\u0378";Assert.That(Assert.Throws<LivLivError>(()=>r.Rasterize(512,256,command)).code,Is.EqualTo("missing-feature"));
        }
        [Test]public void TextMissingGlyphFailsBeforeReplacingTheCurrentModel()
        {
            var files=Synthetic();using(var renderer=new PuppetRenderer())
            {
                renderer.Load(ModelPackage.FromFiles(files));var core=renderer.Core;var model=renderer.Model;
                var replacement=JObject.Parse(Encoding.UTF8.GetString(files["model.json"]));replacement["meta"]=new JObject{["watermark"]=new JObject{["kind"]="text",["text"]="Owner \u0378"}};
                files["model.json"]=Encoding.UTF8.GetBytes(replacement.ToString());
                Assert.That(Assert.Throws<LivLivError>(()=>renderer.Load(ModelPackage.FromFiles(files))).code,Is.EqualTo("missing-feature"));
                Assert.That(renderer.Core,Is.SameAs(core));Assert.That(renderer.Model,Is.SameAs(model));
            }
        }
        [Test]public void TextLongLineClipsAtLeftAndTopBaselinePlacesInkBelowAnchor()
        {
            var r=new UnityMarkText();var c=new BrandDraw{kind="text",text=new string('A',200),x=500,y=100,fontSize=20,textBaseline="bottom"};
            var b=r.Rasterize(512,256,c);Assert.That(b.left,Is.Zero);var font=Resources.Load<Font>("LivLivMarkFont");font.GetCharacterInfo('A',out var glyph,20,FontStyle.Bold);
            Assert.That(b.left+b.width,Is.LessThanOrEqualTo(500+Math.Max(0,glyph.maxX-glyph.advance)+2),"Right alignment uses advance, including glyph overhang and 2px padding");Assert.That(b.alpha.Count(a=>a>.5),Is.GreaterThan(1000));
            c.text="Owner";c.textBaseline="top";var t=r.Rasterize(512,256,c);Assert.That(t.top,Is.GreaterThanOrEqualTo(97));Assert.That(t.alpha.Any(a=>a>.5),Is.True);
        }
        private sealed class CountingText:IMarkTextRasterizer
        {
            public int calls;private readonly UnityMarkText inner=new UnityMarkText();
            public MarkTextCoverage Rasterize(int w,int h,BrandDraw c){calls++;return inner.Rasterize(w,h,c);}
        }
        [Test]public void TextCacheTracksSizeDprAndTextButNotClockAndRestoresActiveTarget()
        {
            var r=new CountingText();var active=RenderTexture.active;bool srgb=GL.sRGBWrite;
            int before=Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name=="LivLiv watermark");
            using(var mark=new UnityMark(r))
            {
                mark.Set("text","Owner");mark.Prepare(512,768,0);mark.Prepare(512,768,5252.5);Assert.That(r.calls,Is.EqualTo(1));
                mark.Prepare(512,768,0,2);mark.Prepare(768,512,0,2);mark.Set("text","New Owner");mark.Prepare(768,512,0,2);Assert.That(r.calls,Is.EqualTo(4));
                Assert.That(RenderTexture.active,Is.EqualTo(active));Assert.That(GL.sRGBWrite,Is.EqualTo(srgb));
            }
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name=="LivLiv watermark"),Is.EqualTo(before));
        }
    }
}
