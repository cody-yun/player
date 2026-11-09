using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class CalibrationTests
    {
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(()=>JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/calibration.json"))));
        public static IEnumerable<TestCaseData> Cases()=>Data.Value["cases"].Select((c,i)=>new TestCaseData(c).SetName("Calibration source scenario "+i+": "+(string)c["name"]));
        private sealed class Store:ICalibrationStore
        {
            public string key,value;public bool getFails,setFails;public int reads;public readonly JArray writes=new JArray();
            public string Get(string k){Assert.That(k,Is.EqualTo(key));reads++;if(getFails)throw new IOException("denied");return value;}
            public void Set(string k,string v){Assert.That(k,Is.EqualTo(key));writes.Add(JObject.Parse(v));if(setFails)throw new IOException("quota");value=v;}
        }
        private static void Compare(JToken actual,JToken expected,string path)
        {
            if(expected is JObject o){foreach(var p in o.Properties())Compare(actual[p.Name],p.Value,path+"."+p.Name);return;}
            if(expected is JArray a){Assert.That(actual.Count(),Is.EqualTo(a.Count),path);for(int i=0;i<a.Count;i++)Compare(actual[i],a[i],path+"["+i+"]");return;}
            if(expected.Type==JTokenType.Float||expected.Type==JTokenType.Integer){double n=(double)expected;Assert.That((double)actual,Is.EqualTo(n).Within(1e-9*Math.Max(1,Math.Abs(n))),path);}
            else Assert.That(JToken.DeepEquals(actual,expected),Is.True,path+": "+actual+" vs "+expected);
        }
        [TestCaseSource(nameof(Cases))]public void PersistenceMatchesOriginal(JToken c)
        {
            var store=new Store{key="lm_calib:"+(string)c["id"],value=(string)c["stored"],getFails=(bool)c["getFails"],setFails=(bool)c["setFails"]};
            var t=new FaceTracker(store,()=>1794210000123){calibId=(string)c["id"]};t.SetMirror((bool)c["mirror"]);
            int index=0;foreach(var row in c["rows"])
            {
                var step=row["step"];switch((string)step["op"])
                {
                    case "restore":t.RestoreCalibration();break;
                    case "frame":t.Poll(step["frame"].ToObject<FaceFrame>(),(double)step["now"]);break;
                    case "mirror":t.SetMirror((bool)step["value"]);break;
                    case "calibrate":t.Calibrate();break;
                    default:throw new Exception("Unknown step");
                }
                Compare(JObject.FromObject(new{t.zero,t.calibrated,t.pos,t.target,t.mirror,store.reads,store.writes}),row["expected"],"step "+index++);
            }
        }
        private sealed class Input:IFaceInput
        {public bool Ready=>true;public double Fps=>0;public bool Dark=>false;public FaceFrame Next(double now)=>null;public void Dispose(){} }
        [Test]public void DriverRestoresBeforeReadingAndPreservesMirrorContract()
        {
            var store=new Store{key="lm_calib:character",value="{\"x\":12,\"y\":-8,\"z\":3,\"px\":6,\"py\":2,\"pz\":-43,\"bl\":0.2,\"br\":0.4}"};
            using(var driver=new FaceTrackingDriver(new Input(),()=>0,new FaceTrackingOptions{mirror=false,remember="character",calibrationStore=store}))
            {Assert.That(store.reads,Is.EqualTo(1));Assert.That(driver.Tracker.calibrated,Is.True);Assert.That(driver.Tracker.zero.x,Is.EqualTo(-12));Assert.That(driver.Tracker.zero.px,Is.EqualTo(-6));}
            Assert.That(store.writes.Count,Is.Zero);
        }
        [Test]public void FixtureUsesCurrentSources()
        {using(var sha=SHA256.Create())foreach(var e in ((JObject)Data.Value["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,e.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)e.Value),e.Name);}
    }
}
