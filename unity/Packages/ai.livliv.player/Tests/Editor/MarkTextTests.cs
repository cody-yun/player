using System;
using LivLiv.Platform;
using LivLiv.Runtime;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class MarkTextTests
    {
        private sealed class Coverage:IMarkTextRasterizer
        {
            public MarkTextCoverage result=new MarkTextCoverage{left=2,top=1,width=2,height=1,alpha=new[]{1f,.5f}};
            public MarkTextCoverage Rasterize(int w,int h,BrandDraw command)=>result;
        }
        private static BrandDraw Text()=>new BrandDraw{kind="text",text="Owner",color="rgba(255,255,255,.85)"};
        [TestCase(-1,16)][TestCase(32,16)][TestCase(16,-1)][TestCase(16,32)]
        public void OffCanvasTextCoverageCastsShadowIntoCanvas(int x,int y)
        {
            // Providers include transparent shadow padding around glyph ink.
            var mask=new float[56*56];mask[(y+12)*56+x+12]=1;
            var provider=new Coverage{result=new MarkTextCoverage{left=-12,top=-12,width=56,height=56,alpha=mask}};
            var command=Text();command.shadowColor="rgba(0,0,0,.6)";command.blur=8;
            var pixels=BrandRasterizer.Draw(32,32,new[]{command},provider);
            Assert.That(Array.Exists(pixels,b=>b>0),Is.True,"Off-canvas glyph coverage must cast a visible shadow");
        }
        [Test]public void TextCoverageCannotExceedShadowHalo()
        {
            var command=Text();command.shadowColor="rgba(0,0,0,.6)";command.blur=8;
            var provider=new Coverage{result=new MarkTextCoverage{left=-13,top=0,width=1,height=1,alpha=new[]{1f}}};
            Assert.That(Assert.Throws<LivLivError>(()=>BrandRasterizer.Draw(32,32,new[]{command},provider)).code,Is.EqualTo("invalid"));
        }
        [Test]public void FontCoverageComposesPremultipliedWhiteAndOpacity()
        {
            var bytes=BrandRasterizer.Draw(8,8,new[]{Text()},new Coverage());
            Assert.That(new[]{bytes[40],bytes[41],bytes[42],bytes[43]},Is.EqualTo(new byte[]{217,217,217,217}));
            Assert.That(new[]{bytes[44],bytes[45],bytes[46],bytes[47]},Is.EqualTo(new byte[]{108,108,108,108}));
            Assert.That(bytes[0],Is.Zero);
        }
        [Test]public void MissingFontProviderIsExplicitInPortableRasterizer()
        {Assert.That(Assert.Throws<LivLivError>(()=>BrandRasterizer.Draw(8,8,new[]{Text()})).code,Is.EqualTo("missing-feature"));}
        [Test]public void InvalidCoverageCannotWriteOutsideCanvasOrInjectNonFiniteAlpha()
        {
            var provider=new Coverage();provider.result.left=8;Assert.Throws<LivLivError>(()=>BrandRasterizer.Draw(8,8,new[]{Text()},provider));
            provider.result.left=0;provider.result.alpha=new[]{float.NaN,1f};Assert.Throws<LivLivError>(()=>BrandRasterizer.Draw(8,8,new[]{Text()},provider));
            provider.result.alpha=new[]{float.PositiveInfinity,1f};Assert.Throws<LivLivError>(()=>BrandRasterizer.Draw(8,8,new[]{Text()},provider));
            provider.result.alpha=Array.Empty<float>();Assert.Throws<LivLivError>(()=>BrandRasterizer.Draw(8,8,new[]{Text()},provider));
        }
    }
}
