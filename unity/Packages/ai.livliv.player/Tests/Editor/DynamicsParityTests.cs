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
    public sealed class DynamicsParityTests
    {
        private static JObject Load()
        {
            using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/dynamics.json.gz")))
            using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(gzip))using(var json=new JsonTextReader(reader))return JObject.Load(json);
        }
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(Load);
        public static IEnumerable<TestCaseData> Cases()=>Data.Value["cases"].Select(c=>new TestCaseData(c).SetName("Puppet dynamics "+c["id"]));
        private static void Compare(JToken actual,JToken expected,string path)
        {
            if(expected is JObject obj){foreach(var p in obj.Properties())Compare(actual[p.Name],p.Value,path+"."+p.Name);return;}
            if(expected is JArray array){Assert.That(actual,Is.TypeOf<JArray>(),path);Assert.That(((JArray)actual).Count,Is.EqualTo(array.Count),path);for(int i=0;i<array.Count;i++)Compare(actual[i],array[i],path+"["+i+"]");return;}
            if(expected.Type==JTokenType.Float||expected.Type==JTokenType.Integer)
            {
                double value=(double)expected;
                // Float32 rope buffers versus double body/spring state. Small trig differences are allowed, not state drift.
                double tolerance=(path.Contains(".chainRuns[")||path.Contains(".deform["))?1e-6*Math.Max(1,Math.Abs(value)):1e-9*Math.Max(1,Math.Abs(value));
                Assert.That((double)actual,Is.EqualTo(value).Within(tolerance),path);
            }
            else Assert.That(JToken.DeepEquals(actual,expected),Is.True,path+": "+actual+" vs "+expected);
        }
        private static Puppet CreatePuppet(JToken test)
        {
            var p=new Puppet();p.LoadDefinition((JObject)test["raw"].DeepClone());
            foreach(var layer in test["layers"])
            {
                var def=((JArray)p.model["layers"]).Cast<JObject>().First(d=>(string)d["id"]==(string)layer["id"]);
                var geometry=p.BuildLayer(def,Convert.FromBase64String((string)layer["alpha"]));
                JsonConvert.PopulateObject(layer["samples"].ToString(),geometry);geometry.outB=new float[geometry.rest.Length];p.layers.Add(geometry);
            }
            return p;
        }
        [Test]
        public void DeformReadsLiveSettingsAfterPreviousFrame()
        {
            var test=Data.Value["cases"][0];var reused=CreatePuppet(test);var fresh=CreatePuppet(test);
            // Hold the pose constant across frames: editing the rig while paused must still work.
            reused.@params.angleX=8;reused.@params.angleY=-3;reused.@params.angleZ=2;reused.@params.breath=.65;
            reused.Deform();var before=reused.layers.SelectMany(l=>l.output).ToArray();
            foreach(var p in new[]{reused,fresh})
            {
                var a=p.model["anchors"];var h=(JObject)a["head"].DeepClone();h["cx"]=(double)h["cx"]+17;a["head"]=h;
                a["neck_y"]=(double)a["neck_y"]+9;
                a["chest"]=new JObject{["cx"]=(double)h["cx"],["cy"]=(double)a["neck_y"]+60,["rx"]=100,["ry"]=130};
                p.rig["bulge_shape"]=1.3;p.rig["bulge"]=(double)p.rig["bulge"]*.7;
                p.rig["bulge_extent"]=new JArray(1.1,.9,1.2,1.05,.8);p.rig["drift"]=new JArray(.14,.09);
                p.tuning["turn"]=.7;p.tuning["nod"]=.6;p.tuning["breath"]=.8;
                p.rig["body"]["bust"]=.6;p.bust.x=4;p.bust.y=-2;p.body.yaw=5;p.body.roll=3;
                p.@params.angleX=8;p.@params.angleY=-3;p.@params.angleZ=2;p.@params.breath=.65;
                p.Deform();
            }
            Assert.That(reused.layers.SelectMany(l=>l.output).ToArray(),Is.Not.EqualTo(before),"Mutation must exercise a different pose");
            for(int i=0;i<reused.layers.Count;i++)
            {
                Assert.That(reused.layers[i].output,Is.EqualTo(fresh.layers[i].output),"Current frame must observe live settings");
                Assert.That(reused.layers[i].outB,Is.EqualTo(fresh.layers[i].outB));
            }
        }
        [TestCaseSource(nameof(Cases))]
        public void TimeSequenceMatchesOriginal(JToken test)
        {
            var p=CreatePuppet(test);int index=0;
            p.keys.AddRange(test["keys"].ToObject<PuppetKey[]>());p.kfPair=test["kfPair"].ToObject<int[]>();
            foreach(var item in ((JObject)test["nasal"]).Properties())p.nasal[item.Name]=item.Value.ToObject<PuppetNasal>();
            foreach(var frame in test["frames"])
            {
                string path=(string)test["id"]+" frame "+index++;
                if((bool)frame["reset"]){p.ResetMouth();p.ResetPhysics();}
                foreach(var v in ((JObject)frame["input"]).Properties())p.@params[v.Name]=(double)v.Value;
                p.expr.Clear();foreach(var v in ((JObject)frame["expr"]).Properties())p.expr[v.Name]=(double)v.Value;
                p.physicsScale=(double)frame["physicsScale"];
                if(frame["tuning"] is JObject tuning)foreach(var v in tuning.Properties())p.tuning[v.Name]=v.Value.DeepClone();
                if(frame["rig"] is JObject rig)foreach(var v in rig.Properties())p.rig[v.Name]=v.Value.DeepClone();
                p.kfOff=(bool)frame["kfOff"];p.keyWeights=frame["keyWeights"].Type==JTokenType.Null?null:frame["keyWeights"].ToObject<double[]>();
                if(frame["poke"] is JArray poke)Assert.That(p.Poke((double)poke[0],(double)poke[1],(double)poke[2]),Is.EqualTo((int)frame["hits"]));
                p.Update((double)frame["dt"]);
                var view=frame["viewCheck"];var place=view["placement"];p.view=place["view"].ToObject<ViewTransform>();p.move=place["move"].ToObject<ViewTransform>();p.zoom=(double)place["zoom"];p.panY=(double)place["panY"];p.focusHead=(bool)place["focusHead"];p.cutBottom=(bool)place["cutBottom"];
                Compare(JArray.FromObject(p.CalculateView((double)view["size"][0],(double)view["size"][1])),view["lastView"],path+".view");
                foreach(var point in view["points"])Compare(JArray.FromObject(p.CanvasToModel((double)point["input"][0],(double)point["input"][1],(double)view["display"][0],(double)view["display"][1])),point["expected"],path+".pick");
                if(frame["deform"] is JArray deform)Compare(JArray.FromObject(p.layers.Select(l=>new {l.output,l.outB,l.soft})),deform,path+".deform");
                var state=JObject.FromObject(new { @params=p.@params,p.time,p.smileShown,p.mouthFrame,p.mouthAmp,p.mouthShape,p.mouthHeld,p.mouthW,p.talkLeft,p.body,p.bust,p.lastHead,p.drive,p.chains,p.chainRuns });
                Compare(state,frame["state"],path);
                int pointIndex=0;
                foreach(var point in test["points"])
                {
                    var head=new double[2];var body=new double[2];var noBust=new double[2];double x=(double)point[0],y=(double)point[1];
                    p.HeadXform(x,y,(double)point[2],p.@params,head);p.BodyOffset(x,y,p.@params,body);p.BodyOffset(x,y,p.@params,noBust,false);
                    Compare(JObject.FromObject(new {head,body,noBust}),frame["probes"][pointIndex++],path+".probe");
                }
                Compare(JArray.FromObject(p.frameKind.Select((_,i)=>p.FrameEase(i,p.@params.mouthOpen))),frame["ease"],path+".ease");
                var visible=new JArray();foreach(JObject def in p.model["layers"])visible.Add(JObject.FromObject(new {id=(string)def["id"],shown=p.Shown(new PuppetLayer{def=def}),expr=p.ExprAlpha(def),mood=p.MoodAlpha(def)}));
                Compare(visible,frame["visible"],path+".visible");
            }
        }
        [Test]
        public void FixtureUsesCurrentSources()
        {
            using(var sha=SHA256.Create())foreach(var p in ((JObject)Data.Value["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value),p.Name);
        }
    }
}
