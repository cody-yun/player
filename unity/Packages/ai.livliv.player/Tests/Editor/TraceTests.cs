using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class TraceTests
    {
        private static JObject Load(){using(var f=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/traces.json.gz")))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var r=new StreamReader(z))return JObject.Parse(r.ReadToEnd());}
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(Load);
        private static void Compare(JToken a,JToken e,string path)
        {
            if(e is JObject o){foreach(var p in o.Properties())Compare(a[p.Name],p.Value,path+"."+p.Name);return;}
            if(e is JArray array){Assert.That(a.Count(),Is.EqualTo(array.Count),path);for(int i=0;i<array.Count;i++)Compare(a[i],array[i],path+"["+i+"]");return;}
            if(e.Type==JTokenType.Null&&a?.Type==JTokenType.Float){Assert.That(double.IsNaN((double)a)||double.IsInfinity((double)a),Is.True,path);return;}
            if(e.Type==JTokenType.Float||e.Type==JTokenType.Integer){double n=(double)e;Assert.That((double)a,Is.EqualTo(n).Within(1e-10*Math.Max(1,Math.Abs(n))),path);return;}
            Assert.That(JToken.DeepEquals(a,e),Is.True,path+": "+a+" vs "+e);
        }
        [TestCase(false),TestCase(true)]public void SynthesisMatchesAllOriginalFrames(bool narrow)
        {Compare(JArray.FromObject(Trace.SynthTrace(narrow)),Data.Value["synth"][narrow?1:0],"synth");}
        public static IEnumerable<TestCaseData> RecordingCases()=>Data.Value["record"].Select((r,i)=>new TestCaseData(r).SetName("Trace recording "+i));
        [TestCaseSource(nameof(RecordingCases))]public void RecordingMatchesOriginal(JToken row)
        {var r=new TraceRecorder();foreach(var f in row["input"])r.Push(f.ToObject<FaceFrame>());Compare(JToken.Parse(r.Save()),row["expected"],"record");}
        [Test]public void RecorderCapAndShallowArraysMatchSource()
        {
            var r=new TraceRecorder();for(int i=0;i<9010;i++)r.Push(new FaceFrame{t=500+i*33});
            Compare(JObject.FromObject(new{length=r.frames.Count,first=r.frames[0].t,last=r.frames.Last().t}),Data.Value["cap"],"cap");
            var f=new FaceFrame{t=100,m=new[]{1d}};r=new TraceRecorder();r.Push(f);f.t=200;f.m[0]=2;
            Assert.That(r.frames[0].t,Is.Zero);Assert.That((double)JArray.Parse(r.Save())[0]["m"][0],Is.EqualTo(2));
            Assert.That(TraceRecorder.FileName(new DateTime(2026,11,9,12,34,56,DateTimeKind.Utc)),Is.EqualTo("face-trace-2026-11-09-12-34-56.json"));
        }
        public static IEnumerable<TestCaseData> StatsCases()=>Data.Value["stats"].Select((r,i)=>new TestCaseData(r).SetName("Trace loop stats "+i));
        [TestCaseSource(nameof(StatsCases))]public void StatisticsMatchOriginalAndCopyFinalParams(JToken row)
        {
            var stats=new SynthStats(()=>(bool)row["mirror"]);foreach(var r in row["rows"])
            {var p=r["p"].ToObject<Params>();stats.Observe((double)r["s"],p);p.angleX=999;Compare(stats.Latest==null?JValue.CreateNull():JObject.FromObject(stats.Latest),r["expected"],"stats");}
        }
        public static IEnumerable<TestCaseData> ReplayCases()=>Data.Value["replay"].Select((r,i)=>new TestCaseData(r).SetName("Trace timed replay "+i));
        [TestCaseSource(nameof(ReplayCases))]public void ReplayMatchesOriginal(JToken row)
        {
            var source=new TraceSource(row["frames"].ToObject<FaceFrame[]>());Assert.That(source.dur,Is.EqualTo((double)row["dur"]));
            foreach(var r in row["rows"]){double now=(double)r["now"];var f=source.Next(now);Compare(f==null?JValue.CreateNull():JObject.FromObject(f),r["frame"],"replay");Assert.That(source.At(now),Is.EqualTo((double)r["at"]));}
        }
        private sealed class Store:ICalibrationStore{public int calls;public string Get(string k){calls++;return "{}";}public void Set(string k,string v){calls++;}}
        [Test]public void ReplayRecordRoundtripPreservesLostFramesAndNeverWritesCameraCalibration()
        {
            var store=new Store();var replay=TraceFaceInput.Synthetic(true);var recording=new RecordingFaceInput(replay);
            using(var driver=new FaceTrackingDriver(recording,()=>0,new FaceTrackingOptions{remember="camera",calibrationStore=store}))
            {
                for(int i=0;i<1440;i++){double now=1000+Math.Floor(i*1000d/30+.5);var f=recording.Next(now);Assert.That(f,Is.Not.Null);driver.Tracker.Poll(f,now);}
                driver.Tracker.Calibrate();Assert.That(store.calls,Is.Zero);Assert.That(driver.Tracker.calibrated,Is.True);
                var read=TraceData.Parse(recording.Recorder.Save());Assert.That(read.Length,Is.EqualTo(1440));Assert.That(read.Count(f=>f.m==null&&f.b==null),Is.EqualTo(120));
                Assert.That(recording.Recorder.frames[0].t,Is.Zero);Assert.That(replay.Fps,Is.EqualTo(30));
            }
            recording.Dispose();Assert.That(replay.Ready,Is.False);Assert.That(recording.Next(99999),Is.Null);Assert.That(recording.Recorder.frames.Count,Is.EqualTo(1440));
        }
        [TestCase("{}"),TestCase("[{\"t\":-1}]"),TestCase("[{\"t\":2},{\"t\":1}]"),TestCase("[{\"t\":0,\"m\":[]}]"),TestCase("[{\"t\":0,\"b\":null}]"),TestCase("[{\"t\":0,}]")]
        public void ExternalMalformedTraceIsRejected(string json){Assert.That(Assert.Throws<LivLivError>(()=>TraceData.Parse(json)).code,Is.EqualTo("invalid"));}
        [Test]public void ExternalFrameBudgetIsEnforced(){Assert.That(Assert.Throws<LivLivError>(()=>TraceData.Parse("["+string.Join(",",Enumerable.Repeat("{\"t\":0}",9001))+"]")).code,Is.EqualTo("too-large"));}
        private sealed class Transport:IModelTransport
        {
            public int calls;public byte[] bytes;public Action during;
            public Task<ModelResponse> Get(Uri url,int maxBytes,bool noCache,CancellationToken token,Action<double> progress=null)
            {calls++;Assert.That(maxBytes,Is.EqualTo(16*1024*1024));during?.Invoke();return Task.FromResult(new ModelResponse{bytes=bytes});}
        }
        [Test]public async Task TraceLoadUsesTransportAndParsesUtf8()
        {var transport=new Transport{bytes=Encoding.UTF8.GetBytes("\ufeff[{\"t\":0}]")};using(var input=await TraceFaceInput.LoadAsync(new Uri("https://example.test/trace.json"),transport)){Assert.That(input.Next(123).t,Is.Zero);Assert.That(transport.calls,Is.EqualTo(1));}}
        [Test]public void TraceLoadHonorsEarlyAndLateCancellation()
        {
            using(var c=new CancellationTokenSource())
            {
                var t=new Transport{bytes=Encoding.UTF8.GetBytes("[]"),during=()=>c.Cancel()};
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),async()=>await TraceFaceInput.LoadAsync(new Uri("https://example.test/trace.json"),t,c.Token));Assert.That(t.calls,Is.EqualTo(1));
                Assert.ThrowsAsync(Is.InstanceOf<OperationCanceledException>(),async()=>await TraceFaceInput.LoadAsync(new Uri("https://example.test/trace.json"),t,c.Token));Assert.That(t.calls,Is.EqualTo(1));
            }
        }
        [Test]public void TraceLoadRejectsInvalidUtf8()
        {var t=new Transport{bytes=new byte[]{255}};Assert.That(Assert.ThrowsAsync<LivLivError>(async()=>await TraceFaceInput.LoadAsync(new Uri("https://example.test/trace.json"),t)).code,Is.EqualTo("invalid"));}
        [TestCase(false),TestCase(true)]public void TwoLoopPipelineMatchesOriginal(bool narrow)
        {
            var input=TraceFaceInput.Synthetic(narrow);var rec=new RecordingFaceInput(input);var tracker=new FaceTracker();var stats=new SynthStats(()=>tracker.mirror);var pose=new Params();int active=0,lost=0;
            using(rec)for(int i=0;i<=1440;i++)
            {double now=1000+Math.Floor(i*1000d/30+.5);var f=rec.Next(now);if(f!=null){if(f.m!=null||f.b!=null)active++;else lost++;}tracker.Poll(f,now);tracker.Apply(pose,1d/30);stats.Observe(input.Source.At(now)/1000,pose);}
            var frames=TraceData.Parse(rec.Recorder.Save());Compare(JObject.FromObject(new{narrow,count=frames.Length,active,lost,fps=input.Fps,stats=stats.Latest,first=frames[0].t,last=frames.Last().t}),Data.Value["pipeline"][narrow?1:0],"pipeline");
        }
        [Test]public void FixtureUsesCurrentSources(){using(var sha=SHA256.Create())foreach(var e in ((JObject)Data.Value["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,e.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)e.Value),e.Name);}
    }
}
