using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LivLiv.Tests
{
    public sealed class PuppetParityTests
    {
        private static JObject Load()
        {
            using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/puppet.json.gz")))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var text=new StreamReader(gzip))using(var json=new JsonTextReader(text))return JObject.Load(json);
        }
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(Load);
        public static IEnumerable<TestCaseData> Layers()=>Data.Value["layers"].Select((r,i)=>new TestCaseData(r).SetName("Puppet layer "+i+": "+r["model"]+"/"+r["id"]));
        private static void Compare(JToken actual,JToken expected,string path="")
        {
            if(expected is JObject obj){foreach(var p in obj.Properties())Compare(actual[p.Name],p.Value,path+"."+p.Name);return;}
            if(expected is JArray array){Assert.That(actual,Is.TypeOf<JArray>(),path);Assert.That(((JArray)actual).Count,Is.EqualTo(array.Count),path);for(int i=0;i<array.Count;i++)Compare(actual[i],array[i],path+"["+i+"]");return;}
            if(expected.Type==JTokenType.Float||expected.Type==JTokenType.Integer){double x=(double)expected;Assert.That((double)actual,Is.EqualTo(x).Within(path.StartsWith(".tri",StringComparison.Ordinal)||path.StartsWith(".rest",StringComparison.Ordinal)?0:1e-6*Math.Max(1,Math.Abs(x))),path);}
            else if(expected.Type==JTokenType.String&&((string)expected=="Infinity"||(string)expected=="NaN"))Assert.That((double)actual,Is.EqualTo((string)expected=="Infinity"?double.PositiveInfinity:double.NaN),path);
            else Assert.That(JToken.DeepEquals(actual,expected),Is.True,path+": "+actual+" vs "+expected);
        }
        [TestCaseSource(nameof(Layers))]
        public void BuildLayerMatchesOriginal(JToken r)
        {
            var m=Data.Value["models"].First(x=>(string)x["id"]==(string)r["model"]);
            var puppet=new Puppet();puppet.LoadDefinition((JObject)m["raw"].DeepClone());
            Compare(JToken.FromObject(puppet.frameKind),r["frameKind"]);Compare(JToken.FromObject(puppet.frameJaw),r["frameJaw"]);
            var def=((JArray)puppet.model["layers"]).Cast<JObject>().First(l=>(string)l["id"]==(string)r["id"]);
            var actual=puppet.BuildLayer(def,Convert.FromBase64String((string)r["alpha"]));
            Compare(JToken.FromObject(actual),r["expected"]);
        }
        [Test]
        public void AlphaGridRejectsWrongDimensionsAndKeepsOneCellHalo()
        {
            Assert.Throws<ArgumentException>(()=>Puppet.AlphaGrid(new byte[1],3,3));
            var alpha=new byte[25];alpha[12]=1;var grid=Puppet.AlphaGrid(alpha,5,5);
            for(int y=0;y<5;y++)for(int x=0;x<5;x++)Assert.That(grid[y*5+x],Is.EqualTo(x>=1&&x<=3&&y>=1&&y<=3?1:0));
        }
        [Test]
        public void PuppetFixtureUsesCurrentSources()
        {
            using(var sha=SHA256.Create())foreach(var p in ((JObject)Data.Value["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value),p.Name);
        }
    }
}
