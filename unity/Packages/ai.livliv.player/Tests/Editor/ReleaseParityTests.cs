using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class ReleaseParityTests
    {
        public static IEnumerable<TestCaseData> Cases()=>JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/release.json")))["cases"]
            .Select(c=>new TestCaseData(c).SetName("Release upstream "+(string)c["name"]));
        [TestCaseSource(nameof(Cases))]public void DeferredReleaseMatchesOriginal(JToken scenario)
        {
            var queue=new List<Action>();bool reject=false;
            var registry=new CanvasRelease(action=>{if(reject){reject=false;throw new Exception("schedule-failed");}queue.Add(action);});
            var surfaces=new Dictionary<string,object>{{"a",new object()},{"b",new object()}};
            var lost=new Dictionary<string,int>{{"a",0},{"b",0}};
            foreach(var row in scenario["rows"])
            {
                string operation=(string)row["operation"],error=null;var parts=operation.Split(':');string kind=parts[0],key=parts.Length>1?parts[1]:null;
                try
                {
                    if(kind=="reject")reject=true;
                    else if(kind=="claim")registry.ClaimCanvas(surfaces[key]);
                    else if(kind=="flush")
                    {var tasks=queue.ToArray();queue.Clear();foreach(var task in tasks)try{task();}catch(Exception e){error=e is LivLivError le?le.code:e.Message;}}
                    else registry.ReleaseCanvas(surfaces[key],kind=="null"?null:(Action)(()=>{if(kind!="missing"){lost[key]++;if(kind=="throw")throw new Exception("backend-failed");}}));
                }
                catch(Exception e){error=e is LivLivError le?le.code:e.Message;}
                Assert.That(error,Is.EqualTo((string)row["error"]),operation);
                Assert.That(JToken.DeepEquals(JObject.FromObject(lost),row["lost"]),Is.True,operation);
            }
        }
    }
}
