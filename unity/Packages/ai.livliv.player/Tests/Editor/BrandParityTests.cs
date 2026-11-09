using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LivLiv.Tests
{
    public sealed class BrandParityTests
    {
        private static JObject Load()=>JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/brand.json")));
        public static IEnumerable<TestCaseData> Draws()=>Load()["draws"].Select((r,i)=>new TestCaseData(r).SetName("Brand canvas "+i+": "+(string)r["type"]));
        private static void Compare(JToken actual,JToken expected,string path="")
        {
            if(expected is JObject o){foreach(var p in o.Properties())Compare(actual[p.Name],p.Value,path+"."+p.Name);return;}
            if(expected is JArray a){Assert.That(((JArray)actual).Count,Is.EqualTo(a.Count),path);for(int i=0;i<a.Count;i++)Compare(actual[i],a[i],path+"["+i+"]");return;}
            if(expected.Type==JTokenType.Float||expected.Type==JTokenType.Integer)Assert.That((double)actual,Is.EqualTo((double)expected).Within(1e-10),path);
            else Assert.That(JToken.DeepEquals(actual,expected),Is.True,path+": "+actual+" vs "+expected);
        }
        [TestCaseSource(nameof(Draws))]
        public void CanvasCommandsMatchOriginal(JToken r)
        {
            double w=(double)r["w"],h=(double)r["h"],ms=(double)r["ms"],px=(double)r["px"];bool top=(bool)r["top"];
            Compare(JToken.FromObject(Brand.CornerPosition(w,h,top,px)),r["spot"]);
            var commands=new List<BrandDraw>();
            switch((string)r["type"])
            {
                case "watermark":Brand.DrawWatermark(commands,w,h,ms,px,top);break;
                case "text":Brand.DrawMarkText(commands,w,h,"Creator 이름",px,top);break;
                default:Brand.DrawCornerLogo(commands,w,h,ms,px,top,(string)r["type"]=="outline");break;
            }
            Compare(JToken.FromObject(commands),r["commands"]);
        }
        [Test]
        public void BlinkAndMarkCacheMatchOriginal()
        {
            var data=Load();foreach(var r in data["eyes"])Assert.That(Brand.EyeOpen((double)r["ms"]),Is.EqualTo((double)r["value"]).Within(1e-12));
            var mark=new Mark();string key=null;List<BrandDraw> previous=null;
            foreach(var r in data["marks"])
            {
                mark.Set((string)r["kind"],(string)r["text"],(bool)r["center"]);Assert.That(mark.Visible,Is.EqualTo((bool)r["visible"]));
                if(!mark.Visible)continue;
                var next=mark.CacheKey((int)r["w"],(int)r["h"],(double)r["px"],(double)r["ms"]);
                Assert.That(next,Is.EqualTo((string)r["key"]));
                if(next!=key){previous=mark.Draw((int)r["w"],(int)r["h"],(double)r["ms"],(double)r["px"]);key=next;}
                Compare(JToken.FromObject(previous),r["commands"]);
            }
        }
        [Test]
        public void BrandFixtureUsesCurrentSources()
        {
            using(var sha=SHA256.Create())foreach(var p in ((JObject)Load()["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value),p.Name);
        }
    }
}
