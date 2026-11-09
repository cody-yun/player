// Port of src/runtime/trace.ts: frames, replay, recorder, synthesis and loop statistics.
// Copyright 2026 LivLiv. Apache-2.0. DOM download/window globals become returned JSON/results.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LivLiv.Runtime
{
    [Serializable]
    public sealed class FaceFrame
    {
        // Milliseconds; column-major 4x4, blendshapes in BS order, x/y pairs in LM order.
        public double t;
        public double[] m, b, l;
    }
    public static class Trace
    {
        public static readonly string[] BS = {
            "eyeBlinkLeft", "eyeBlinkRight", "jawOpen", "eyeLookOutLeft", "eyeLookInRight", "eyeLookInLeft", "eyeLookOutRight",
            "eyeLookUpLeft", "eyeLookUpRight", "eyeLookDownLeft", "eyeLookDownRight", "browInnerUp", "browOuterUpLeft",
            "browOuterUpRight", "browDownLeft", "browDownRight", "mouthSmileLeft", "mouthSmileRight"
        };
        public static readonly int[] LM = { 10, 13, 14, 152 };
        public static FaceFrame[] SynthTrace(bool narrow=false)
        {
            double seed=7;
            double Rnd(){seed=(seed*16807)%2147483647;return seed/2147483647;}
            double Gauss()=>Math.Sqrt(-2*Math.Log(Rnd()+1e-9))*Math.Cos(2*Math.PI*Rnd());
            double rest=narrow?.38:.05;var frames=new List<FaceFrame>();
            for(int k=0;k<24*30;k++)
            {
                double s=k/30d,t=Math.Floor(k*1000d/30+.5);
                if(s>=10&&s<12){frames.Add(new FaceFrame{t=t});continue;}
                double yaw=0,pitch=0,roll=0;
                if(s>=4&&s<8)yaw=22*Math.Sin(Math.PI*(s-4));
                if(s>=8&&s<10)pitch=18*Math.Min(1,(s-8)/.3);
                if(s>=15&&s<18)yaw=20*Math.Min(1,(s-15)/.3);
                yaw+=Gauss()*.5;pitch+=Gauss()*.5;roll+=Gauss()*.4;
                double bl=rest+Math.Abs(Gauss())*.02,br=rest+Math.Abs(Gauss())*.02;
                int bk=(int)Math.Floor((s-13)*30+.5);if(bk>=0&&bk<4)bl=br=rest+new[]{.3,.57,.4,.15}[bk];
                if(s>=15.3&&s<18)br=.35+Math.Abs(Gauss())*.25;
                double jaw=s>=18&&s<21?.45*Math.Max(0,Math.Sin(2*Math.PI*3.5*s)):.02;
                var b=BS.Select(n=>n=="eyeBlinkLeft"?bl:n=="eyeBlinkRight"?br:n=="jawOpen"?jaw:n.StartsWith("eyeLook",StringComparison.Ordinal)?Math.Abs(Gauss())*.04:n=="mouthSmileLeft"||n=="mouthSmileRight"?.1:.02).ToArray();
                double lean=s>=21?Math.Min(1,(s-21)/.6):0;
                frames.Add(new FaceFrame{t=t,m=Rotation(yaw,pitch,roll,new[]{6*lean+Gauss()*.1,Gauss()*.1,-45+5*lean+Gauss()*.2}),b=b,l=new[]{.5,.25,.5,.62,.5,.62+jaw*.05,.5,.8}});
            }
            return frames.ToArray();
        }
        private static double[] Rotation(double yaw,double pitch,double roll,double[] pos)
        {
            double r=Math.PI/180,a=pitch*r,b=yaw*r,c=roll*r;
            double ca=Math.Cos(a),sa=Math.Sin(a),cb=Math.Cos(b),sb=Math.Sin(b),cc=Math.Cos(c),sc=Math.Sin(c);
            return new[]{cc*cb,sc*cb,-sb,0,cc*sb*sa-sc*ca,sc*sb*sa+cc*ca,cb*sa,0,cc*sb*ca+sc*sa,sc*sb*ca-cc*sa,cb*ca,0,pos[0],pos[1],pos[2],1};
        }

    }
    public sealed class TraceRecorder
    {
        public readonly List<FaceFrame> frames=new List<FaceFrame>();
        private double t0=-1;
        public void Push(FaceFrame f)
        {
            if(t0<0)t0=f.t;
            if(frames.Count<30*60*5)frames.Add(new FaceFrame{t=Math.Floor(f.t-t0+.5),m=f.m,b=f.b,l=f.l});
        }
        // save() payload; the host selects file/download UI. Arrays keep source shallow-copy semantics.
        public string Save()
        {
            JToken Number(double n)=>double.IsNaN(n)||double.IsInfinity(n)?JValue.CreateNull():new JValue(n==0?0:n);
            JArray R4(double[] a)=>new JArray(a.Select(v=>Number(Math.Floor(v*1e4+.5)/1e4)));
            var all=new JArray();foreach(var f in frames)
            {var row=new JObject{["t"]=Number(f.t)};if(f.m!=null)row["m"]=R4(f.m);if(f.b!=null)row["b"]=R4(f.b);if(f.l!=null)row["l"]=R4(f.l);all.Add(row);}
            return all.ToString(Formatting.None);
        }
        public static string FileName(DateTime utc)=>"face-trace-"+utc.ToUniversalTime().ToString("yyyy-MM-dd-HH-mm-ss",System.Globalization.CultureInfo.InvariantCulture)+".json";
    }
    public sealed class TraceSource
    {
        private double t0 = -1;
        private int i = -1;
        public readonly IReadOnlyList<FaceFrame> frames;
        public readonly bool synth;
        public readonly double dur;
        public TraceSource(IReadOnlyList<FaceFrame> frames, bool synth = false)
        {
            this.frames = frames; this.synth = synth;
            dur = frames.Count > 0 ? frames[frames.Count - 1].t + 33 : 0;
        }
        public double At(double now) => t0 < 0 || dur == 0 ? 0 : (now - t0) % dur;
        public FaceFrame Next(double now)
        {
            if (frames.Count == 0) return null;
            if (t0 < 0) t0 = now;
            var at = At(now);
            int j = i;
            if (j >= 0 && frames[j].t > at) j = -1;
            while (j + 1 < frames.Count && frames[j + 1].t <= at) j++;
            if (j == i || j < 0) return null;
            i = j;
            return frames[j];
        }
    }
    [Serializable]public sealed class SynthStatsResult
    {
        public double stillJitterX,stillJitterY,stillMeanY,stillEyeOpen,turnLagMs,turnRmsDeg,lostMotionDeg,lostEyeOpen,lostMeanY,blinkMinOpen,farEyeWinkShare;
        public int loops;
    }
    public sealed class SynthStats
    {
        private sealed class Row{public double s;public Params p;}
        private readonly List<Row> rows=new List<Row>();private double lastS=-1;private readonly Func<bool> mirror;
        public double At {get;private set;}
        public SynthStatsResult Latest {get;private set;}
        public SynthStats(Func<bool> mirror){this.mirror=mirror??throw new ArgumentNullException(nameof(mirror));}
        public void Observe(double s,Params p)
        {
            At=s;if(s<lastS){Publish();rows.Clear();}lastS=s;rows.Add(new Row{s=s,p=p.Clone()});
        }
        private static double Mean(IEnumerable<double> values){var a=values.ToArray();return a.Sum()/a.Length;}
        private static double Sd(IEnumerable<double> values){var a=values.ToArray();double m=Mean(a);return Math.Sqrt(a.Sum(v=>(v-m)*(v-m))/a.Length);}
        private static double SynthYaw(double s)=>s>=4&&s<8?22*Math.Sin(Math.PI*(s-4)):s>=15&&s<18?20*Math.Min(1,(s-15)/.3):0;
        private static double R2(double v)=>Math.Floor(v*100+.5)/100;
        private void Publish()
        {
            if(rows.Count==0||rows[rows.Count-1].s<23)return;
            Row[] Seg(double a,double b)=>rows.Where(r=>r.s>=a&&r.s<b).ToArray();
            var still=Seg(1.5,4).Concat(Seg(21.5,24)).ToArray();var turn=Seg(4.5,8);double sign=mirror()?-1:1,bestLag=0,bestErr=double.PositiveInfinity;
            for(double lag=0;lag<=.4;lag+=.008)
            {
                double err=Mean(turn.Select(r=>Math.Pow(r.p.angleX-sign*1.3*SynthYaw(r.s-lag),2)));
                if(err<bestErr){bestLag=lag;bestErr=err;}
            }
            var lost=Seg(10.8,12);var far=Seg(15.6,18);var blink=Seg(12.9,13.6);
            Latest=new SynthStatsResult{stillJitterX=R2(Sd(still.Select(r=>r.p.angleX))),stillJitterY=R2(Sd(still.Select(r=>r.p.angleY))),
                stillMeanY=R2(Mean(still.Select(r=>r.p.angleY))),stillEyeOpen=R2(Mean(still.Select(r=>(r.p.eyeLOpen+r.p.eyeROpen)/2))),
                turnLagMs=Math.Floor(bestLag*1000+.5),turnRmsDeg=R2(Math.Sqrt(bestErr)),lostMotionDeg=R2(Sd(lost.Select(r=>r.p.angleX))+Sd(lost.Select(r=>r.p.angleY))),
                lostEyeOpen=R2(Mean(lost.Select(r=>Math.Min(r.p.eyeLOpen,r.p.eyeROpen)))),lostMeanY=R2(Mean(lost.Select(r=>r.p.angleY))),
                blinkMinOpen=R2(blink.Length==0?double.PositiveInfinity:blink.Min(r=>Math.Max(r.p.eyeLOpen,r.p.eyeROpen))),
                farEyeWinkShare=R2(far.Count(r=>Math.Abs(r.p.eyeLOpen-r.p.eyeROpen)>.25)/(double)Math.Max(1,far.Length)),loops=(Latest?.loops??0)+1};
        }
    }
}
