// Port of src/runtime/idle.ts. Copyright 2026 LivLiv. Apache-2.0.
// Random source is injectable; pointer/display coordinates are supplied by the Unity host.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LivLiv.Runtime
{
    public sealed class IdlePoint { public double x,y; }
    public sealed class IdleShown { public double x,z; }
    public sealed class IdlePointer { public double x,y,d=99;public bool on,keep; }
    public sealed class IdleStyle
    {
        public double wander=1,look=1,moves=1,bodyTurn,bodyTilt,smile=.4;
        public string pointer="attend";
    }
    public sealed class IdleMoveState { public string id;public double t,side,fade; }
    public sealed partial class LivelyIdle
    {
        public double nextBlink=2,blinkT=-1,blinkAt=-99,blinkRate=1,nextSaccade=1,moves=1,nextMove=6;
        public IdlePoint look=new IdlePoint(),gaze=new IdlePoint();
        public IdleShown shown=new IdleShown();
        public IdlePointer pointer=new IdlePointer();
        public IdleStyle style=new IdleStyle();
        public bool busy,tracked,faceFree=true;
        public IdleMoveState move;
        public string last="";
        private bool smiling,doubleBlink;
        private sealed class Attention
        {
            public double px,py,moved=-99,speed,since,x,y,soft,side=1,notice=-99,back=-99,smile=-99,smiled=-99;
            public bool on,engaged;
            public IdlePoint slow=new IdlePoint(),jitter=new IdlePoint();
        }
        private sealed class Spring { public double x,v; }
        private readonly Attention att=new Attention();
        private double folX,folZ,folX2,folZ2,breathPhase,breathPeriod=3.8;
        private readonly double seed;
        private readonly Func<double> random;
        private static readonly string[] SpringKeys={"angleX","angleY","angleZ","bodyX","bodyZ"};
        private static readonly double[][] Springs={new[]{55d,.72},new[]{55d,.72},new[]{45d,.75},new[]{14d,.9},new[]{12d,.9}};
        private readonly Spring[] springs=Enumerable.Range(0,5).Select(_=>new Spring()).ToArray();
        private double[] aim;
        public LivelyIdle(Func<double> random=null){this.random=random??new Random().NextDouble;seed=this.random()*100;}
        private static double Smooth(double a,double b,double x)=>Params.Smooth(a,b,x);
        private static double Clamp(double x,double a,double b)=>Math.Max(a,Math.Min(b,x));
        private static double Hypot(double x,double y)=>Math.Sqrt(x*x+y*y);
        private static double Knee(double v,double k,double cap){double a=Math.Abs(v);return a<=k?v:Math.Sign(v)*(k+(cap-k)*Math.Tanh((a-k)/(cap-k)));}
        private static double Env(double s,double a,double h,double d)=>s<0||s>a+h+d?0:s<a?Smooth(0,a,s):1-Smooth(a+h,a+h+d,s);
        private static double Noise(double t,double seed)
        {
            double Hash(double n){double x=Math.Sin(n*127.1+seed*311.7)*43758.5453;return x-Math.Floor(x);}
            double i=Math.Floor(t),f=t-i,u=f*f*(3-2*f);return (Hash(i)*(1-u)+Hash(i+1)*u)*2-1;
        }
        private static double Fbm(double t,double seed)=>Noise(t,seed)*.7+Noise(t*2.3,seed+17)*.3;
        private static double Sample(JArray keys,double t)
        {
            if(t<=(double)keys[0][0])return (double)keys[0][1];int n=keys.Count-1;if(t>=(double)keys[n][0])return (double)keys[n][1];int i=0;while(t>(double)keys[i+1][0])i++;
            double Slope(int j)=>j<=0||j>=n?0:((double)keys[j+1][1]-(double)keys[j-1][1])/((double)keys[j+1][0]-(double)keys[j-1][0]);
            double h=(double)keys[i+1][0]-(double)keys[i][0],u=(t-(double)keys[i][0])/h,u2=u*u,u3=u2*u;
            return (2*u3-3*u2+1)*(double)keys[i][1]+(u3-2*u2+u)*Slope(i)*h+(-2*u3+3*u2)*(double)keys[i+1][1]+(u3-u2)*Slope(i+1)*h;
        }
        public static void PointAt(LivelyIdle idle,Puppet puppet,double x,double y,double width,double height,bool on)
        {
            var head=puppet.model?["anchors"]?["head"];var v=puppet.lastView;bool drawn=head!=null&&v[4]>1;
            double fx=drawn?((double)head["cx"]*v[0]+v[2]+1)/2:.5,fy=drawn?((double)head["cy"]*v[1]+v[3]+1)/2:.3;
            double rx=drawn?Math.Abs((double)head["rx"]*v[0])/2:.12,ry=drawn?Math.Abs((double)head["ry"]*v[1])/2:.1,u=x/Math.Max(1,width),w=y/Math.Max(1,height);
            idle.pointer=new IdlePointer{x=Clamp((u-fx)*2,-1,1),y=Clamp(-(w-fy)*1.6,-1,1),d=Hypot((u-fx)/rx,(w-fy)/ry),on=on};
        }
        private double Breath(double dt)
        {
            breathPhase+=dt/breathPeriod;if(breathPhase>=1){breathPhase-=1;breathPeriod=3.2+random()*1.4;}
            double x=breathPhase;return x<.4?Smooth(0,.4,x):x<.88?1-Smooth(.4,.88,x):0;
        }
        public void Apply(Params p,double t,double dt)
        {
            dt=dt>0?Math.Min(dt,.1):0;var st=style;bool attend=st.pointer=="attend"&&!tracked;if(attend)Attend(t,dt);
            bool quiet=busy||attend&&att.engaged;
            if(move!=null){move.t+=dt;if(quiet)move.fade=Math.Max(0,move.fade-dt/.4);if(move.t>=(double)Moves[move.id]["dur"]||move.fade<=0)move=null;}
            else if(quiet)nextMove=Math.Max(nextMove,t+3);
            else if(moves>0&&t>nextMove){var ids=Moves.Properties().Select(e=>e.Name).Where(k=>k!=last).ToArray();string id=ids[(int)Math.Floor(random()*ids.Length)];move=new IdleMoveState{id=id,side=random()<.5?-1:1,fade=1};last=id;nextMove=t+(double)Moves[id]["dur"]+10+random()*10;}
            var m=move;var definition=m!=null?Moves[m.id]:null;double weight=m!=null?m.fade*Smooth(0,.15,m.t)*(1-Smooth((double)definition["dur"]-.25,(double)definition["dur"],m.t)):0;
            double Mv(string k,double rest=0)
            {
                var track=definition?["tracks"]?[k] as JArray;if(track==null)return rest;
                double value=Sample(track,m.t),mirror=k=="angleX"||k=="angleZ"||k=="bodyX"||k=="bodyZ"||k=="eyeBallX"?m.side:1;
                return rest+(value*mirror-rest)*weight*moves*(k.StartsWith("angle",StringComparison.Ordinal)||k.StartsWith("body",StringComparison.Ordinal)?st.moves:1);
            }
            double open=1;if(blinkT<0&&t>nextBlink){blinkT=0;blinkAt=t;}
            if(blinkT>=0)
            {
                blinkT+=dt;double b=blinkT/.075;open=Clamp(b<1?1-b:b<2.6?(b-1)/1.6:1,0,1);
                if(b>=2.6){blinkT=-1;if(!doubleBlink&&random()<.18){doubleBlink=true;nextBlink=t+.08;}else{doubleBlink=false;nextBlink=t+(1.8+random()*3.8)/Math.Max(.2,blinkRate);}}
            }
            double eye=Clamp(Mv("eye",1),0,1)*open;p.eyeLOpen=p.eyeROpen=attend?eye*(1-.16*att.soft):eye;
            double sd=seed,lk=st.look,wd=st.wander,gx,gy;double[] target;
            if(attend)
            {
                var a=att;if(t>nextSaccade){a.jitter=new IdlePoint{x=(random()-.5)*.2,y=(random()-.5)*.16};nextSaccade=t+.6+random()*1.6;}
                gaze=new IdlePoint{x=a.x+a.jitter.x,y=.8*a.y+a.jitter.y};double ex=gaze.x+Mv("eyeBallX"),ey=gaze.y+Mv("eyeBallY"),e=Hypot(ex/.78,ey/.55),k=e>.7?Knee(e,.7,1)/e:1;gx=ex*k;gy=ey*k;
                var slow=a.slow;if(Hypot(gx-slow.x,gy-slow.y)>.5){if(blinkT<0&&t-blinkAt>1.2)nextBlink=Math.Min(nextBlink,t);slow.x=gx;slow.y=gy;}
                slow.x+=(gx-slow.x)*Math.Min(1,dt/.35);slow.y+=(gy-slow.y)*Math.Min(1,dt/.35);
                double nod=moves>0?3*Env(t-a.notice,.12,.15,.5)-2.5*Env(t-a.back,.15,.1,.45):0;
                target=new[]{Knee(a.x*9*lk+Fbm(t*.17,sd)*4*wd+gaze.x*2+Mv("angleX"),10,13),Knee(a.y*5*lk+Fbm(t*.13,sd+1)*3*wd+gaze.y*2+nod+Mv("angleY"),6,10),Knee(a.x*2*lk+a.side*3*a.soft+Fbm(t*.11,sd+2)*5*wd+Mv("angleZ"),8,12),a.x*1.5*lk+Fbm(t*.09,sd+3)*3*wd+Mv("bodyX"),Fbm(t*.08,sd+4)*2.5*wd+Mv("bodyZ")};
            }
            else
            {
                if(t>nextSaccade){gaze=new IdlePoint{x=look.x+(random()-.5)*.25,y=look.y*.8+(random()-.5)*.2};nextSaccade=t+.6+random()*1.6;}
                gx=Clamp(gaze.x*1.2+Mv("eyeBallX"),-1,1);gy=Clamp(gaze.y+Mv("eyeBallY"),-1,1);
                target=new[]{look.x*20*lk+Fbm(t*.17,sd)*7*wd+gaze.x*5+Mv("angleX"),look.y*13*lk+Fbm(t*.13,sd+1)*4*wd+gaze.y*3+Mv("angleY"),-look.x*4*lk+Fbm(t*.11,sd+2)*6*wd+Mv("angleZ"),look.x*4*lk+Fbm(t*.09,sd+3)*3*wd+Mv("bodyX"),Fbm(t*.08,sd+4)*2.5*wd+Mv("bodyZ")};
            }
            p.eyeBallX+=(gx-p.eyeBallX)*Math.Min(1,dt*18);p.eyeBallY+=(gy-p.eyeBallY)*Math.Min(1,dt*18);
            int steps=Math.Max(1,(int)Math.Ceiling(dt/(1d/120)));if(aim==null)aim=(double[])target.Clone();
            for(int k=0;k<5;k++)
            {
                double stiffness=Springs[k][0],damping=2*Math.Sqrt(stiffness)*Springs[k][1];var spring=springs[k];
                for(int i=0;i<steps;i++){aim[k]+=(target[k]-aim[k])*Math.Min(1,dt/steps/.12);spring.v+=(stiffness*(aim[k]-spring.x)-damping*spring.v)*(dt/steps);spring.x+=spring.v*(dt/steps);}p[SpringKeys[k]]=spring.x;
            }
            double follow=Math.Min(1,dt/.12);folX+=(st.bodyTurn*shown.x-folX)*follow;folX2+=(folX-folX2)*follow;folZ+=(st.bodyTilt*shown.z-folZ)*follow;folZ2+=(folZ-folZ2)*follow;
            if(st.bodyTurn!=0||st.bodyTilt!=0){p.bodyX=Clamp(p.bodyX+folX2,-10,10);p.bodyZ=Clamp(p.bodyZ+folZ2,-10,10);}
            p.breath=Clamp(Mv("breath",Breath(dt)),0,1);
            double brow=(attend?AttendBrow(t):look.y>.4?.5:0)-(1-open)*.15+Mv("brow");p.browLY+=(brow-p.browLY)*Math.Min(1,dt*10);p.browRY=p.browLY;
            if(attend){p.smile=t>=att.smile&&t<att.smile+1.6?1:0;smiling=p.smile>0;}else if(smiling){p.smile=0;smiling=false;}
        }
        private void Attend(double t,double dt)
        {
            var a=att;var q=pointer;
            if(q.on&&!a.on){a.notice=t;a.moved=t;a.px=q.x;a.py=q.y;a.side=random()<.5?-1:1;}
            if(q.on){double step=Hypot(q.x-a.px,q.y-a.py);a.speed+=(step/Math.Max(dt,1e-3)-a.speed)*Math.Min(1,dt/.15);if(step>.003)a.moved=t;a.px=q.x;a.py=q.y;}else a.speed*=Math.Max(0,1-dt/.15);
            a.on=q.on;bool onFace=q.on&&q.d<1,engaged=q.on&&(onFace||q.keep||t-a.moved<2.2);
            if(engaged!=a.engaged){if(!engaged&&t-a.since>.8){a.back=t;nextMove=Math.Max(nextMove,t+6);}a.since=t;a.engaged=engaged;}
            if(onFace&&t-a.moved>1.2&&style.smile>0&&faceFree&&!busy&&t-a.smiled>25&&t>a.smile+1.6){if(a.smiled<0||random()<style.smile)a.smile=t;a.smiled=t;}
            bool isSmiling=t>=a.smile&&t<a.smile+1.6;double soft=isSmiling?0:Math.Max(onFace&&t-a.moved>.5?1:0,Env(t-a.back,.2,.6,.6));a.soft+=(soft-a.soft)*Math.Min(1,dt/.25);a.x=engaged?q.x:0;a.y=engaged?q.y:0;
        }
        private double AttendBrow(double t)
        {
            var a=att;var q=pointer;double near=q.on&&t-a.moved<.3?1-Smooth(1,2.2,q.d):0;
            return Math.Min(.7,.4*Smooth(.35,.9,a.y)+.3*Smooth(1.5,5,a.speed)+.15*near+.45*Env(t-a.notice,.08,.25,.35)+.2*Env(t-a.back,.15,.3,.5)+.12*a.soft);
        }
    }
}

namespace LivLiv.Runtime { public sealed partial class LivelyIdle {
    // IDLE_MOVES literal from upstream; fixture checks its content and source hash.
    public static readonly Newtonsoft.Json.Linq.JObject Moves=Newtonsoft.Json.Linq.JObject.Parse(@"{""glance"":{""dur"":4,""tracks"":{""eyeBallX"":[[0,0],[0.15,0.8],[2.5,0.75],[2.8,0]],""eyeBallY"":[[0,0],[0.15,0.25],[2.5,0.2],[2.8,0]],""angleX"":[[0,0],[0.6,11],[2.5,10],[3.4,0]],""angleY"":[[0,0],[0.6,4],[2.5,3],[3.4,0]],""angleZ"":[[0,0],[0.7,-4],[2.5,-3],[3.4,0]],""bodyX"":[[0,0],[1,2],[2.6,1.5],[3.8,0]],""brow"":[[0,0],[0.4,0.25],[2.5,0.2],[3.2,0]],""breath"":[[0,0.1],[0.6,0.7],[2.6,0.6],[3.6,0],[4,0.15]]}},""lookAround"":{""dur"":5,""tracks"":{""eyeBallX"":[[0,0],[0.2,-0.8],[1.4,-0.75],[1.7,0.1],[2,0.8],[3.2,0.75],[3.5,0]],""angleX"":[[0,0],[0.7,-15],[1.5,-14],[2.4,15],[3.3,14],[4.2,0]],""angleY"":[[0,0],[0.8,3],[2.4,3],[4.2,0]],""angleZ"":[[0,0],[0.8,3],[1.7,0],[2.5,-3],[4.2,0]],""bodyX"":[[0,0],[1,-3],[2.7,3],[4.4,0]],""brow"":[[0,0],[0.5,0.3],[3.4,0.25],[4.2,0]],""breath"":[[0,0.2],[0.6,0.8],[3.4,0.7],[4.4,0],[5,0.1]]}},""sway"":{""dur"":6,""tracks"":{""bodyZ"":[[0,0],[1.5,3.5],[3,-3.5],[4.5,3],[6,0]],""bodyX"":[[0,0],[1.5,2],[3,-2],[4.5,1.5],[6,0]],""angleZ"":[[0,0],[1.6,-7],[3.1,7],[4.6,-6],[6,0]],""angleX"":[[0,0],[1.6,4],[3.1,-4],[4.6,3],[6,0]],""eyeBallX"":[[0,0],[1.6,-0.25],[3.1,0.25],[4.6,-0.2],[6,0]],""eye"":[[0,1],[0.8,0.88],[5.2,0.88],[6,1]],""breath"":[[0,0],[0.75,1],[1.5,0],[2.25,1],[3,0],[3.75,1],[4.5,0],[5.25,0.8],[6,0]]}},""think"":{""dur"":4.4,""tracks"":{""eyeBallX"":[[0,0],[0.3,-0.6],[3.4,-0.55],[3.8,0]],""eyeBallY"":[[0,0],[0.3,0.6],[3.4,0.55],[3.8,0]],""angleX"":[[0,0],[0.8,-9],[3.4,-8],[4.2,0]],""angleY"":[[0,0],[0.8,6],[3.4,5],[4.2,0]],""angleZ"":[[0,0],[0.9,6],[3.4,5],[4.2,0]],""brow"":[[0,0],[0.6,0.35],[3.4,0.3],[4.2,0]],""eye"":[[0,1],[0.6,0.92],[3.4,0.92],[4,1]]}},""lookDown"":{""dur"":2.4,""tracks"":{""eyeBallY"":[[0,0],[0.2,-0.85],[1.4,-0.8],[1.65,0]],""eyeBallX"":[[0,0],[0.2,-0.1],[1.4,-0.1],[1.65,0]],""angleY"":[[0,0],[0.45,-8],[1.4,-7],[1.9,1],[2.4,0]],""angleZ"":[[0,0],[0.5,2],[1.5,2],[2.4,0]],""eye"":[[0,1],[0.3,0.8],[1.4,0.8],[1.65,1]]}}}");
} }
