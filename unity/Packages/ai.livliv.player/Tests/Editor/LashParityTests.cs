using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class LashParityTests
    {
        public static JArray Load()
        {
            using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/lash.json.gz")))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var text=new StreamReader(gzip))using(var json=new JsonTextReader(text))return (JArray)JObject.Load(json)["samples"];
        }
        [Test]
        public void FixtureUsesCurrentSources()
        {
            using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/lash.json.gz")))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var text=new StreamReader(gzip))
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var source in ((JObject)JObject.Parse(text.ReadToEnd())["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,source.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)source.Value),source.Name);
        }
        public static IEnumerable<TestCaseData> Cases()=>Load().Select(row=>new TestCaseData(row).SetName("Closed lash "+row["model"]+"/"+row["def"]["id"]));
        [TestCaseSource(nameof(Cases))]
        public void CurveMatchesOriginalCanvasPixels(JToken row)
        {
            var line=Puppet.ClosedLashLine(Convert.FromBase64String((string)row["rgba"]),(int)row["width"],(int)row["height"],(JObject)row["def"]);
            Assert.That(line==null,Is.EqualTo(row["expected"].Type==JTokenType.Null));if(line==null)return;
            for(int i=0;i<((JArray)row["xs"]).Count;i++)Assert.That(line((double)row["xs"][i]),Is.EqualTo((double)row["expected"][i]).Within(1e-9));
        }
    }
}
