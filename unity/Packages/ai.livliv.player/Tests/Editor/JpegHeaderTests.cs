using System;
using System.IO;
using LivLiv.Platform;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class JpegHeaderTests
    {
        [TestCase("baseline")][TestCase("progressive")]
        public void ReadsRealFilesAndRejectsEveryTruncatedHeader(string kind)
        {
            var bytes=File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-"+kind+".jpg"));
            JpegHeader.Read(bytes,out int w,out int h);Assert.That(w,Is.EqualTo(32));Assert.That(h,Is.EqualTo(48));
            int firstValid=0;
            for(int i=0;i<bytes.Length;i++)
            {
                var prefix=new byte[i];Array.Copy(bytes,prefix,i);
                try{JpegHeader.Read(prefix,out w,out h);firstValid=i;break;}catch(LivLivError e){Assert.That(e.code,Is.EqualTo("invalid"));}
            }
            Assert.That(firstValid,Is.GreaterThan(10));Assert.That(firstValid,Is.LessThan(bytes.Length));
        }
        [Test] public void RejectsInvalidLengthsAndScanWithoutFrame()
        {
            foreach(var bytes in new[]{new byte[]{255,216,255,224,0,0},new byte[]{255,216,255,224,255,255},new byte[]{255,216,255,218},new byte[]{255,216,255}})
                Assert.Throws<LivLivError>(()=>JpegHeader.Read(bytes,out _,out _));
        }
    }
}
