using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using LivLiv.Platform;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class PackageSourceParityTests
    {
        [TestCase(true),TestCase(false)]public void CanceledPackageSelectionDoesNotInspectInput(bool blob)
        {
            using(var source=new CancellationTokenSource())
            {
                source.Cancel();
                Assert.Throws<OperationCanceledException>(()=>
                {
                    if(blob)ModelPackage.FromZip(new byte[0],cancellation:source.Token);
                    else ModelPackage.FromFiles(new Dictionary<string,byte[]>(),lang:"en",cancellation:source.Token);
                });
            }
        }
        public static IEnumerable<TestCaseData> Cases()=>JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/package-source.json")))["cases"]
            .Select(c=>new TestCaseData(c).SetName("Package source upstream "+(string)c["name"]));
        [TestCaseSource(nameof(Cases))]public void SelectedPackageMatchesUpstream(JToken scenario)
        {
            ModelPackage package=null;LivLivError error=null;
            try
            {
                if((string)scenario["kind"]=="blob")package=ModelPackage.FromZip(Convert.FromBase64String((string)scenario["bytes"]));
                else
                {
                    var files=new Dictionary<string,byte[]>(StringComparer.Ordinal);
                    foreach(var p in ((JObject)scenario["files"]).Properties())files.Add(p.Name,Convert.FromBase64String((string)p.Value));
                    package=ModelPackage.FromFiles(files);
                }
            }
            catch(LivLivError e){error=e;}
            var expected=scenario["expected"];
            Assert.That(error?.code,Is.EqualTo((string)expected["error"]));
            if(error!=null)Assert.That(error.Message,Is.EqualTo((string)expected["message"]));
            else
            {
                Assert.That(JToken.DeepEquals(package.model,expected["model"]),Is.True,"normalized model");
                Assert.That(JToken.DeepEquals(package.keyforms??JValue.CreateNull(),expected["keyforms"]),Is.True,"keyforms");
            }
        }
    }
}
