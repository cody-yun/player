using System;
using System.IO;
using LivLiv.Platform;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class SurfaceSizeTests
    {
        [Test] public void MatchesOriginalRoundingAndDensityAcross96Cases()
        {
            var data=JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/surface-size.json")));
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var p in ((JObject)data["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value));
            foreach(var row in data["cases"])
            {
                var s=SurfaceSize.Calculate((double)row["width"],(double)row["height"],(double)row["density"],(double)row["cap"]);
                Assert.That(new[]{s.width,s.height},Is.EqualTo(new[]{(int)row["expected"][0],(int)row["expected"][1]}),row.ToString());Assert.That(s.limited,Is.False);
            }
        }
        [TestCase(10000,10000,4096,4194304)]
        [TestCase(1000000,1,8192,100)]
        [TestCase(1,1000000,8192,100)]
        [TestCase(1024,768,64,2000)]
        public void BudgetBoundsBothSidesAndArea(int w,int h,int side,int pixels)
        {
            var s=SurfaceSize.Calculate(w,h,2,2,side,pixels);
            Assert.That(s.limited,Is.True);Assert.That(s.width,Is.InRange(1,side));Assert.That(s.height,Is.InRange(1,side));Assert.That((long)s.width*s.height,Is.LessThanOrEqualTo(pixels));
        }
        [Test] public void InvalidValuesFailBeforeAllocation()
        {
            foreach(var x in new[]{double.NaN,double.PositiveInfinity,-1})Assert.Throws<ArgumentOutOfRangeException>(()=>SurfaceSize.Calculate(x,10,1));
            foreach(var x in new[]{0,double.NaN,double.PositiveInfinity,-1})Assert.Throws<ArgumentOutOfRangeException>(()=>SurfaceSize.Calculate(10,10,1,x));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SurfaceSize.Calculate(10,10,-1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SurfaceSize.Calculate(double.MaxValue,10,1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>SurfaceSize.Calculate(10,10,1,2,8192,0));
        }
    }
}
