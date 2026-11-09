using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class AlphaSamplingTests
    {
        private static JObject Load()
        {
            using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/alpha-sampling.json.gz")))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var text=new StreamReader(gzip))using(var json=new JsonTextReader(text))return JObject.Load(json);
        }
        public static IEnumerable<TestCaseData> Cases()=>Load()["samples"].Select(r=>new TestCaseData(r).SetName("Alpha sampling: "+r["id"]+"/"+r["lid"]));
        [Test]
        public void SamplingFixtureUsesCurrentSourcesAndImages()
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var p in ((JObject)Load()["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value),p.Name);
        }
        [TestCaseSource(nameof(Cases))]
        public void MatchesCanvasOccupancy(JToken r)
        {
            var model=ModelFormat.ReadModel((JObject)r["raw"].DeepClone());var def=((JArray)model["layers"]).Cast<JObject>().First(d=>(string)d["id"]==(string)r["lid"]);
            var inputs=r["inputs"].Select(i=>new AlphaImage((int)i["width"],(int)i["height"],Convert.FromBase64String((string)i["alpha"]))).ToArray();
            var grid=new LayerLattice(model,def);var actual=Puppet.AlphaGrid(AlphaRaster.Grid(inputs,grid),grid.cols,grid.rows);var wanted=Convert.FromBase64String((string)r["expected"]);
            var errors=actual.Select((a,i)=>a!=wanted[i]?i:-1).Where(i=>i>=0).ToArray();Assert.That(errors,Is.Empty,string.Join(",",errors.Select(i=>$"({i%grid.cols},{i/grid.cols}):{actual[i]}/{wanted[i]}")));
        }
    }
}
