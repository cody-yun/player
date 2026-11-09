using System.Collections.Generic;
using LivLiv.Platform;
using LivLiv.Runtime;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class BrandClipTests
    {
        // Blurring a shape and then clipping must equal cropping the same shape from a larger canvas.
        // This exercises shadow contributions from ink just beyond each canvas edge.
        [TestCase(-3,16)][TestCase(35,16)][TestCase(16,-3)][TestCase(16,35)]
        [TestCase(0,0)][TestCase(32,32)]
        public void ShadowAtCanvasEdgeMatchesCropOfLargerCanvas(double x,double y)
        {
            var commands=new[]{new BrandDraw{kind="fill",color="#fff",shadowColor="rgba(0,0,0,.6)",blur=8,ellipse=new[]{x,y,7d,7d}}};
            AssertCrop(commands,32,32);
        }
        [TestCase(24,64)][TestCase(64,24)][TestCase(32,32)]
        public void UpstreamCornerLogoInSmallCanvasMatchesLargerCanvasCrop(int width,int height)
        {
            var commands=new List<BrandDraw>();Brand.DrawCornerLogo(commands,width,height,0);
            AssertCrop(commands,width,height);
        }
        private static void AssertCrop(IReadOnlyList<BrandDraw> commands,int width,int height)
        {
            const int border=32;
            var actual=BrandRasterizer.Draw(width,height,commands);
            foreach(var command in commands){command.tx+=border;command.ty+=border;}
            var expanded=BrandRasterizer.Draw(width+border*2,height+border*2,commands);
            var expected=new byte[actual.Length];
            for(int y=0;y<height;y++)System.Buffer.BlockCopy(expanded,((y+border)*(width+border*2)+border)*4,expected,y*width*4,width*4);
            Assert.That(actual,Is.EqualTo(expected),"Off-canvas ink must contribute its shadow before final clipping");
        }
    }
}
