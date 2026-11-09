using System.IO;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed class JpegGraphicsTests
    {
        [TestCase("baseline")][TestCase("progressive")]
        public void RealJpegPreservesDimensionsColorAndUnityRowDirection(string kind)
        {
            var bytes=File.ReadAllBytes(Path.Combine(Application.dataPath,"../Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-"+kind+".jpg"));
            var decoder=new TextureDecoder();Texture2D image=null;
            try
            {
                image=decoder.Decode(bytes);Assert.That(image.width,Is.EqualTo(32));Assert.That(image.height,Is.EqualTo(48));
                var pixels=image.GetPixels32();var bottom=pixels[8*32+16];var top=pixels[40*32+16];
                Assert.That(bottom.b,Is.GreaterThan(245));Assert.That(bottom.r,Is.LessThan(10));
                Assert.That(top.r,Is.GreaterThan(245));Assert.That(top.b,Is.LessThan(10));
                Assert.That(bottom.a,Is.EqualTo(255));Assert.That(top.a,Is.EqualTo(255));Assert.That(decoder.texels,Is.EqualTo(32*48));
            }
            finally{TextureDecoder.Destroy(image);}
        }
        [Test] public void OversizeHeaderIsRejectedBeforePixelDecode()
        {
            var bytes=File.ReadAllBytes(Path.Combine(Application.dataPath,"../Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-baseline.jpg"));
            for(int i=2;i<bytes.Length-8;i++)if(bytes[i]==255&&bytes[i+1]==192){bytes[i+7]=255;bytes[i+8]=255;break;}
            var decoder=new TextureDecoder();var error=Assert.Throws<LivLivError>(()=>decoder.Decode(bytes));
            Assert.That(error.code,Is.EqualTo("too-large"));Assert.That(decoder.texels,Is.Zero);
        }
    }
}
