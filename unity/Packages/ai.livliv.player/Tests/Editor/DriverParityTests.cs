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
    public sealed class DriverParityTests
    {
        private static JObject Load(){using(var f=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/drivers.json.gz")))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var t=new StreamReader(z))using(var j=new JsonTextReader(t))return JObject.Load(j);}
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(Load);
        public static IEnumerable<TestCaseData> TalkCases()=>Data.Value["talk"].Select((r,i)=>new TestCaseData(r).SetName("Talk sequence "+i));
        public static IEnumerable<TestCaseData> DriverCases()=>Data.Value["drivers"].Select((r,i)=>new TestCaseData(r).SetName("Face and voice drivers "+i));
        private static void Compare(JToken a,JToken e,string path)
        {
            if(e is JObject o){foreach(var p in o.Properties())Compare(a[p.Name],p.Value,path+"."+p.Name);return;}
            if(e.Type==JTokenType.Float||e.Type==JTokenType.Integer){double v=(double)e;Assert.That((double)a,Is.EqualTo(v).Within(1e-9*Math.Max(1,Math.Abs(v))),path);}
            else Assert.That(JToken.DeepEquals(a,e),Is.True,path+": "+a+" vs "+e);
        }
        [TestCaseSource(nameof(TalkCases))] public void TalkMatchesOriginal(JToken row)
        {
            uint seed=72813;int calls=0,index=0;double Random(){seed=unchecked(seed*1664525+1013904223);calls++;return seed/4294967296d;}
            var talk=new TalkMotion(Random){amount=(double)row["amount"]};
            foreach(var f in row["rows"]){talk.bounce=(bool)f["bounce"];if((bool)f["reset"])talk.Reset();var offsets=talk.Update((double)f["level"],(double)f["dt"],f["p"].ToObject<Params>());Compare(JObject.FromObject(new{offsets,talk.act,talk.hop,talk.pauseBlink,talk.stats,calls}),f["expected"],"talk["+index+++"]");}
        }
        private sealed class FaceInput : IFaceInput
        {public bool ready=true;public FaceFrame input;public int stops;public bool Ready=>ready;public double Fps=>30;public bool Dark=>false;public FaceFrame Next(double now)=>input;public void Dispose(){stops++;ready=false;}}
        private sealed class VoiceInput : IVoiceInput
        {public double raw,vowel,level;public int stops;public double Read(){level=raw*Gain;return level;}public double Vowel=>vowel;public double Level=>level;public double Gain{get;set;}=1;public void Dispose(){stops++;}}
        private sealed class FrameSurface : PlayerBase
        {
            public double now;public PlayerFrame Frame=>frame;private readonly Dictionary<string,double> expr=new Dictionary<string,double>();
            public FrameSurface():base(new Params(),new PlayerOptions{idle=false},()=>.5){}
            protected override bool Drawable=>true;protected override double Clock=>now/1000;
            protected override IDictionary<string,double> ShownExpressions=>expr;protected override IEnumerable<string> ExpressionIds=>Array.Empty<string>();
            protected override Puppet Seen=>null;protected override double DisplayWidth=>1;protected override double DisplayHeight=>1;
            protected override void Show(double dt){}protected override void Free(){}
        }
        private sealed class Host : IPlayerHost
        {
            private readonly List<IPlayerDriver> drivers=new List<IPlayerDriver>();public int blinks;
            public Action Use(IPlayerDriver d){drivers.Add(d);drivers.Sort((a,b)=>a.Order.CompareTo(b.Order));return ()=>{if(drivers.Remove(d))d.Dispose();};}
            public void Blink(){blinks++;}public void Update(PlayerFrame frame){foreach(var d in drivers)d.Update(frame,this);}
        }
        [TestCaseSource(nameof(DriverCases))] public void CombinedDriversMatchOriginal(JToken row)
        {
            uint seed=72813;int calls=0,index=0;double Random(){seed=unchecked(seed*1664525+1013904223);calls++;return seed/4294967296d;}
            var host=new Host();var faceInput=new FaceInput();var voiceInput=new VoiceInput();var o=row["options"];
            using(var surface=new FrameSurface())using(var face=FaceTracking.Attach(host,faceInput,()=>surface.now,new FaceTrackingOptions{follow=(double)o["follow"],smoothing=.7}))
            using(var voice=LipSync.Attach(host,voiceInput,o.ToObject<LipSyncOptions>(),Random))
            foreach(var f in row["rows"])
            {
                surface.now=(double)f["now"];faceInput.ready=(bool)f["ready"];faceInput.input=f["input"].Type==JTokenType.Null?null:f["input"].ToObject<FaceFrame>();
                if(index==480||index==1100)face.Mirror=!face.Mirror;if(index==550)face.Calibrate();if(index==740)voice.Gain=.7;
                if(index==1400){face.Stop();face.Stop();}if(index==1550){voice.Stop();voice.Stop();}
                voiceInput.raw=(double)f["voiceLevel"];voiceInput.vowel=(double)f["voiceVowel"];
                foreach(var entry in ((JObject)f["initial"]).Properties())surface.@params[entry.Name]=(double)entry.Value;
                surface.Tick((double)f["dt"]);host.Update(surface.Frame);var frame=surface.Frame;
                Compare(JObject.FromObject(new{@params=frame.@params,move=frame.move,tracked=frame.tracked,face=face.Face,mirror=face.Mirror,level=voice.Level,gain=voice.Gain,blinks=host.blinks,calls,faceStops=faceInput.stops,voiceStops=voiceInput.stops}),f["expected"],"drivers["+index+++"]");
            }
        }
        [Test]public void FixtureUsesCurrentSources(){using(var sha=SHA256.Create())foreach(var e in ((JObject)Data.Value["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,e.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)e.Value),e.Name);}
    }
}
