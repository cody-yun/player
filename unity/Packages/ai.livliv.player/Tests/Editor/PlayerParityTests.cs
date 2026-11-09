using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class PlayerParityTests
    {
        private static JObject Load(){using(var file=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/player.json.gz")))using(var gzip=new GZipStream(file,CompressionMode.Decompress))using(var reader=new StreamReader(gzip))using(var json=new JsonTextReader(reader))return JObject.Load(json);}
        private sealed class Surface : PlayerBase
        {
            public double clock;public int draws,frees;
            public readonly Dictionary<string,double> expressions=new Dictionary<string,double>();
            public readonly Puppet seen=new Puppet();
            public PlayerFrame Frame=>frame;public double ActionY=>actions.y;
            public Surface(Func<double> random,JObject data):base(new Params(),new PlayerOptions{autoplay=false},random)
            {seen.LoadDefinition(JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"characters/d01/model.json"))));seen.model["anchors"]["head"]=data["seen"]["model"]["anchors"]["head"].DeepClone();seen.lastView=data["seen"]["lastView"].ToObject<double[]>();}
            protected override bool Drawable=>true;
            protected override double Clock=>clock;
            protected override IDictionary<string,double> ShownExpressions=>expressions;
            protected override IEnumerable<string> ExpressionIds=>new[]{"smile","shy","angry"};
            protected override Puppet Seen=>seen;
            protected override double DisplayWidth=>768;
            protected override double DisplayHeight=>1024;
            protected override void Show(double dt){clock+=dt;draws++;}
            protected override void Free(){frees++;}
        }
        private sealed class Driver : PlayerDriver
        {public int priority;public Action<PlayerFrame> update;public Action release;public override int Order=>priority;public override void Update(PlayerFrame frame,IPlayerHost player)=>update(frame);public override void Dispose()=>release();}
        private static void Compare(JToken actual,JToken expected,string path)
        {
            if(expected is JObject obj){foreach(var entry in obj.Properties())Compare(actual[entry.Name],entry.Value,path+"."+entry.Name);return;}
            if(expected is JArray array){Assert.That(actual.Count(),Is.EqualTo(array.Count),path);for(int i=0;i<array.Count;i++)Compare(actual[i],array[i],path+"["+i+"]");return;}
            if(expected.Type==JTokenType.Float||expected.Type==JTokenType.Integer){double v=(double)expected;Assert.That((double)actual,Is.EqualTo(v).Within(1e-9*Math.Max(1,Math.Abs(v))),path);}
            else Assert.That(JToken.DeepEquals(actual,expected),Is.True,path+": "+actual+" vs "+expected);
        }
        [Test] public void DriverControlsAndLifetimeMatchOriginal()
        {
            var data=Load();uint rng=987654321;int calls=0,index=0;
            double Random(){rng=unchecked(rng*1664525+1013904223);calls++;return rng/4294967296d;}
            using(var p=new Surface(Random,data))
            {
                var removers=new Dictionary<string,Action>();var disposed=new Dictionary<string,int>();var order=new List<string>();
                void Add(string id,int priority)
                {
                    disposed[id]=0;removers[id]=p.Use(new Driver{priority=priority,release=()=>disposed[id]++,update=f=>{
                        order.Add(id);var q=f.@params;
                        if(id=="track"){q.angleX=13+Math.Sin(index*.2);f.tracked=true;f.move.x+=.02;}
                        else if(id=="offset")f.Add(new Dictionary<string,double>{{"angleX",2},{"angleY",index%37==0?double.NaN:3},{"eyeLOpen",-.1}});
                        else if(id=="scale"){q.angleX*=1.2;q.bodyZ=2;f.move.s=1+f.time*.01;}
                        else if(id=="voice"){q.mouthOpen=.4+.3*Math.Sin(index*.3);if(!f.tracked)q.angleY+=5;f.move.y=.02;}
                        else if(id=="invalid"){q.eyeBallX=double.NaN;q.angleZ=index%2!=0?double.PositiveInfinity:double.NegativeInfinity;}
                        if(id=="offset"&&index==80)removers[id]();
                    }});
                }
                foreach(var row in data["frames"])
                {
                    var results=new JArray();foreach(var e in row["events"])
                    {
                        string kind=(string)e[0];switch(kind){case "add":Add((string)e[1],(int)e[2]);break;case "remove":removers[(string)e[1]]();break;
                            case "expression":results.Add(p.SetExpression((string)e[1],(double)e[2]));break;case "act":p.Act((string)e[1]);break;case "away":p.SetAway((bool)e[1]);break;
                            case "idle":p.idle=(bool)e[1];break;case "blink":p.Blink();break;case "look":p.LookAt(e[1].Type==JTokenType.Null?(double?)null:(double)e[1],(double)e[2]);break;
                            case "leave":p.PointerLeave();break;case "play":p.Play();break;case "pause":p.Pause();break;case "dispose":p.Dispose();break;default:throw new Exception(kind);}
                    }
                    p.@params.angleX=9;p.@params.mouthOpen=.2;p.@params.smile=.1;p.visible=(bool)row["visible"];order.Clear();p.Tick((double)row["elapsed"]);
                    Assert.That(JToken.DeepEquals(results,row["results"]),Is.True,"control results "+index);
                    Compare(JObject.FromObject(new{@params=p.Frame.@params,user=p.@params,move=p.Frame.move,dt=p.Frame.dt,time=p.Frame.time,tracked=p.Frame.tracked,clock=p.clock,expressions=p.expressions,actionY=p.ActionY,playing=p.Playing,draws=p.draws,frees=p.frees,disposed,order,randomCalls=calls}),row["expected"],"frame["+index+++"]");
                }
            }
        }
        [Test] public void FixtureUsesCurrentSources(){using(var sha=SHA256.Create())foreach(var entry in ((JObject)Load()["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,entry.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)entry.Value),entry.Name);}
    }
}
