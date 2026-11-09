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
    public sealed class IdleParityTests
    {
        private static JObject Load()
        {
            using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/idle.json.gz")))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var text=new StreamReader(gzip))using(var json=new JsonTextReader(text))return JObject.Load(json);
        }
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(Load);
        public static IEnumerable<TestCaseData> Cases()=>Data.Value["cases"].Select(row=>new TestCaseData(row).SetName("Idle sequence "+row["kind"]));
        private static void Compare(JToken actual,JToken expected,string path)
        {
            if(expected is JObject obj){foreach(var entry in obj.Properties())Compare(actual[entry.Name],entry.Value,path+"."+entry.Name);return;}
            if(expected.Type==JTokenType.Integer||expected.Type==JTokenType.Float){double value=(double)expected;Assert.That((double)actual,Is.EqualTo(value).Within(1e-9*Math.Max(1,Math.Abs(value))),path);}
            else Assert.That(JToken.DeepEquals(actual,expected),Is.True,path+": "+actual+" vs "+expected);
        }
        [TestCaseSource(nameof(Cases))]
        public void TimeSequenceMatchesOriginal(JToken row)
        {
            string kind=(string)row["kind"];uint state=123456789;int calls=0;
            double Random(){state=unchecked(state*1664525+1013904223);calls++;return state/4294967296d;}
            var wrapper=kind=="follow"?new Idle(Random):null;var classic=kind=="classic"?new IdleDriver(Random):null;var lively=wrapper?.lv??(classic==null?new LivelyIdle(Random):null);
            if(kind=="turn")lively.style=new IdleStyle{pointer="turn",bodyTurn=.35,bodyTilt=.12,look=.7,wander=.6,moves=.8};if(kind=="reduced")lively.moves=0;
            var puppet=new Puppet();puppet.LoadDefinition(JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"characters/d01/model.json"))));puppet.model["anchors"]["head"]=Data.Value["seen"]["model"]["anchors"]["head"].DeepClone();puppet.lastView=Data.Value["seen"]["lastView"].ToObject<double[]>();
            var pose=new Params();int index=0;
            foreach(var f in row["frames"])
            {
                double dt=(double)f["dt"],time=(double)f["time"];bool busy=(bool)f["busy"],tracked=(bool)f["tracked"],faceFree=(bool)f["faceFree"];
                if(wrapper!=null)
                {
                    wrapper.Shown(pose);var e=f["event"];string eventKind=(string)e["kind"];
                    if(eventKind=="look")wrapper.LookAt(puppet,e["x"].Type==JTokenType.Null?(double?)null:(double)e["x"],(double)e["y"],768,1024);
                    else if(eventKind=="leave")wrapper.Leave();else if(eventKind=="blink")wrapper.Blink(time);else wrapper.Pointer(puppet,(double)e["x"],(double)e["y"],768,1024,(bool)e["on"]);
                    pose.smile=index%240<40?.8:.15;pose.mouthOpen=.2;wrapper.Apply(pose,time,dt,busy,tracked,faceFree);
                }
                else if(classic!=null){classic.look=f["look"].ToObject<IdlePoint>();classic.Apply(pose,time,dt);}
                else
                {
                    lively.look=f["look"].ToObject<IdlePoint>();lively.busy=busy;lively.tracked=tracked;lively.faceFree=faceFree;lively.shown=new IdleShown{x=pose.angleX+2,z=pose.angleZ-1};lively.pointer=f["pointer"].ToObject<IdlePointer>();
                    if(f["forced"] is JObject)lively.move=f["forced"].ToObject<IdleMoveState>();lively.Apply(pose,time,dt);
                }
                string path=kind+"["+index+++"]";Compare(JObject.FromObject(pose),f["expected"],path);Assert.That(calls,Is.EqualTo((int)f["calls"]),path+" random calls");
                Compare(classic!=null?JObject.FromObject(classic):JObject.FromObject(lively),f["state"],path+".state");
            }
        }
        [Test]
        public void MoveTracksMatchOriginal(){Assert.That(JToken.DeepEquals(LivelyIdle.Moves,Data.Value["moves"]),Is.True);}
        [Test]
        public void FixtureUsesCurrentSources()
        {
            using(var sha=SHA256.Create())foreach(var entry in ((JObject)Data.Value["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,entry.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)entry.Value),entry.Name);
        }
    }
}
