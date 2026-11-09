using System;
using System.IO;
using System.Linq;
using LivLiv.Platform;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class MaskBoundsTests
    {
        [Test] public void BoundsAndGrowthMatch160OriginalCases()
        {
            var fixture=JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/mask-bounds.json")));
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var p in ((JObject)fixture["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value));
            foreach(var c in fixture["cases"])
            {
                var v=c["view"].ToObject<double[]>();var b=MaskBounds.Calculate(c["points"].Select(a=>a.ToObject<float[]>()),(int)c["w"],(int)c["h"],v[0],v[1],v[2],v[3]);
                var e=c["expected"];if(e.Type==JTokenType.Null){Assert.That(b.Renderable,Is.False,c.ToString());continue;}
                Assert.That(new[]{b.x,b.y,b.width,b.height},Is.EqualTo(new[]{(int)e["x"],(int)e["y"],(int)e["width"],(int)e["height"]}),c.ToString());
                Assert.That(new[]{MaskBounds.Grow((int)c["previous"][0],b.width),MaskBounds.Grow((int)c["previous"][1],b.height)},Is.EqualTo(e["storage"].ToObject<int[]>()));
            }
        }
    }
}
