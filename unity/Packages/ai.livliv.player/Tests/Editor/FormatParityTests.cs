using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LivLiv.Tests
{
    public sealed class FormatParityTests
    {
        private static JObject Load()
        {
            using (var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/format.json.gz")))
            using (var gzip=new GZipStream(file,CompressionMode.Decompress))
            using (var text=new StreamReader(gzip))
            using (var reader=new JsonTextReader(text) { DateParseHandling=DateParseHandling.None }) return JObject.Load(reader);
        }
        public static IEnumerable<TestCaseData> Models() => Load()["models"].Select(r=>new TestCaseData(r).SetName("Model: "+(string)r["name"]));
        public static IEnumerable<TestCaseData> Zips() => Load()["zip"].Select(r=>new TestCaseData(r).SetName("ZIP: "+(string)r["name"]));
        [TestCaseSource(nameof(Models))]
        public void ModelMatchesReference(JToken r)
        {
            var input=r["input"].DeepClone(); var before=input.DeepClone();
            if (r["result"]!=null)
            {
                var actual=ModelFormat.ReadModel(input,(string)r["lang"]);
                Assert.That(JToken.DeepEquals(actual,r["result"]),Is.True,"actual: "+actual);
                Assert.That(ReferenceEquals(actual,input),Is.EqualTo((bool)r["same"]));
                Assert.That(ModelFormat.ReadModel(actual),Is.SameAs(actual));
                Assert.That(JToken.DeepEquals(input,before),Is.True,"reader mutated the input");
            }
            else
            {
                var error=Assert.Throws<LivLivError>(()=>ModelFormat.ReadModel(input,(string)r["lang"]));
                // Upstream malformed JS operations can throw an uncoded TypeError; the C# boundary normalizes to invalid.
                if (r["error"].Type!=JTokenType.Null) Assert.That(error.code,Is.EqualTo((string)r["error"]));
            }
        }
        [TestCaseSource(nameof(Zips))]
        public void ZipMatchesReference(JToken r)
        {
            var bytes=Convert.FromBase64String((string)r["bytes"]);
            Assert.That(LivLivZip.IsZip(bytes),Is.EqualTo((bool)r["isZip"]));
            if (r["files"] is JObject expected)
            {
                var actual=LivLivZip.Unzip(bytes);
                CollectionAssert.AreEqual(expected.Properties().Select(p=>p.Name),actual.Keys);
                foreach (var p in expected.Properties()) CollectionAssert.AreEqual(Convert.FromBase64String((string)p.Value),actual[p.Name]);
            }
            else
            {
                var error=Assert.Throws<LivLivError>(()=>LivLivZip.Unzip(bytes));
                if (r["error"].Type!=JTokenType.Null) Assert.That(error.code,Is.EqualTo((string)r["error"]));
            }
        }
        [Test]
        public void FormatSourceHashesStillMatch()
        {
            foreach (var p in ((JObject)Load()["sourceHashes"]).Properties())
            {
                using (var sha=SHA256.Create())
                {
                    var hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant();
                    Assert.That(hash,Is.EqualTo((string)p.Value),p.Name);
                }
            }
        }
        [Test]
        public void LabelsLanguageAndLatticeMatch()
        {
            var data=Load();
            foreach (var r in data["labels"]) Assert.That(ModelFormat.PickLabel(r["labels"] as JObject,(string)r["lang"],"fallback"),Is.EqualTo((string)r["expected"]));
            foreach (var r in data["languages"]) Assert.That(ModelFormat.LangOf((string)r["text"]),Is.EqualTo((string)r["expected"]));
            foreach (var r in data["lattice"]) Assert.That(ModelFormat.LatticeCell((double)r["w"],(double)r["h"]),Is.EqualTo((int)r["expected"]));
        }
        [Test]
        public void FolderAndPickedFilesPreserveMetadataAndResolveLegacyImages()
        {
            var dir=Path.Combine(Path.GetTempPath(),"livliv-package-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try
            {
                var model=(JObject)Load()["models"].First(r=>(string)r["name"]=="legacy")["input"].DeepClone();
                model["layers"][0]["webp"]=true;model["keyforms"]="keyforms.json";
                var bytes=Encoding.UTF8.GetBytes(model.ToString()); var image=new byte[]{1,2,3};
                File.WriteAllBytes(Path.Combine(dir,"model.json"),bytes);Directory.CreateDirectory(Path.Combine(dir,"textures"));
                File.WriteAllBytes(Path.Combine(dir,"textures/body.webp"),image);
                var folder=ModelPackage.FromDirectory(dir);
                CollectionAssert.AreEqual(image,folder.ReadImage((JObject)folder.model["layers"][0],"textures/body.png"));
                Assert.That(folder.warnings.Count,Is.EqualTo(1));
                Assert.That(Assert.Throws<LivLivError>(()=>folder.ReadImage((JObject)folder.model["layers"][0],"textures/body.png",true)).code,Is.EqualTo("missing-file"));
                var picked=ModelPackage.FromFiles(new Dictionary<string,byte[]> { ["Folder/model.json"]=bytes,["body.webp"]=image });
                CollectionAssert.AreEqual(image,picked.ReadImage((JObject)picked.model["layers"][0],"textures/body.png"));
                Assert.That(JToken.DeepEquals(folder.model,picked.model),Is.True);
                Assert.That(Assert.Throws<LivLivError>(()=>folder.ReadImage((JObject)folder.model["layers"][0],"../secret")).code,Is.EqualTo("invalid"));
            }
            finally { Directory.Delete(dir,true); }
        }
        [Test]
        public void JsonDatesStayTextAndTrailingDocumentsAreRejected()
        {
            var raw=(JObject)Load()["models"].First(r=>(string)r["name"]=="minimal-v4")["input"].DeepClone();
            raw["meta"]["created"]="2026-11-09T00:00:00Z";
            var text=raw.ToString();
            var package=ModelPackage.FromFiles(new Dictionary<string,byte[]> { ["model.json"]=Encoding.UTF8.GetBytes(text) });
            Assert.That(package.model["meta"]["created"].Type,Is.EqualTo(JTokenType.String));
            Assert.That(Assert.Throws<LivLivError>(()=>ModelPackage.FromFiles(new Dictionary<string,byte[]> { ["model.json"]=Encoding.UTF8.GetBytes(text+"{}") })).code,Is.EqualTo("not-a-model"));
        }
        [Test]
        public void NestedDeflatedLivLivPackageLoadsWithoutExtraction()
        {
            var model=Load()["models"].First(r=>(string)r["name"]=="minimal-v4")["input"];
            using(var bytes=new MemoryStream())
            {
                using(var archive=new ZipArchive(bytes,ZipArchiveMode.Create,true))
                {
                    using(var writer=new StreamWriter(archive.CreateEntry("Wrapped/model.json").Open(),new UTF8Encoding(false)))writer.Write(model.ToString());
                    using(var entry=archive.CreateEntry("Wrapped/textures/body.png").Open())entry.Write(new byte[]{1,2,3},0,3);
                }
                var package=ModelPackage.FromZip(bytes.ToArray());
                Assert.That((string)package.model["name"],Is.EqualTo("Synthetic"));
                CollectionAssert.AreEqual(new byte[]{1,2,3},package.ReadImage((JObject)package.model["layers"][0],"textures/body.png"));
            }
        }
        [TestCase("{/* comment */}"),TestCase("{'x':1}"),TestCase("{\"x\":1,}"),TestCase("[1,]"),TestCase("{\"x\":NaN}"),TestCase("{\"x\":undefined}"),TestCase("{\"x\":01}"),TestCase("{\"x\":.5}")]
        public void PackageUsesJsonParseGrammar(string json)
        {
            Assert.That(Assert.Throws<LivLivError>(()=>ModelPackage.FromFiles(new Dictionary<string,byte[]> { ["model.json"]=Encoding.UTF8.GetBytes(json) })).code,Is.EqualTo("not-a-model"));
        }
    }
}
