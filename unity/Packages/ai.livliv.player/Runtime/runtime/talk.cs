// Port of src/runtime/talk.ts. Copyright 2026 LivLiv. Apache-2.0.
using System;
using System.Collections.Generic;
namespace LivLiv.Runtime
{
    public sealed class TalkStats { public int onsets,nods,brows,blinks; }
    public sealed class TalkMotion
    {
        private sealed class Spring { public double x,v; }
        public double amount=1,act,hop;
        public bool bounce,pauseBlink;
        public readonly TalkStats stats=new TalkStats();
        private bool armed=true;
        private double valley,peak,quiet=10,sinceNod=10,sinceBrow=10,browAge=10,t;
        private Spring nod=new Spring(),lift=new Spring();
        private IdlePoint dir=new IdlePoint(),aim=new IdlePoint();
        private readonly double[] phase;
        private readonly Dictionary<string,double> off=new Dictionary<string,double>();
        private readonly Func<double> random;
        public TalkMotion(Func<double> random=null){this.random=random??new Random().NextDouble;phase=new[]{this.random()*9,this.random()*9,this.random()*9};}
        private static void Step(Spring s,double k,double c,double dt){s.v+=(-k*s.x-c*s.v)*dt;s.x+=s.v*dt;}
        public IReadOnlyDictionary<string,double> Update(double level,double dt,Params p)
        {
            t+=dt;quiet+=dt;sinceNod+=dt;sinceBrow+=dt;pauseBlink=false;double was=act;
            if(level>.2)quiet=0;
            act+=((quiet<.35?1:0)-act)*Math.Min(1,dt*(quiet<.35?6:1.2));
            if(was>.6&&act<=.6&&quiet>.4){pauseBlink=true;stats.blinks++;}
            if(!armed){peak=Math.Max(peak,level);if(level<peak-.15){armed=true;valley=level;}}
            else
            {
                valley=Math.Min(valley,level);
                if(level-valley>.25&&level>.35)
                {
                    armed=false;peak=level;stats.onsets++;bool phrase=act<.3;
                    if(phrase)aim=new IdlePoint{x=(random()*2-1)*5,y=(random()-.3)*2.5};
                    if(sinceNod>.35&&(phrase||random()<.35)){nod.v-=(phrase?70:35+random()*30)*amount;sinceNod=0;stats.nods++;}
                    if(sinceBrow>1.2&&random()<(phrase?.5:.18)){browAge=0;sinceBrow=0;stats.brows++;}
                    if(bounce)lift.v+=(.3+random()*.12)*amount;
                }
            }
            int n=Math.Max(1,(int)Math.Ceiling(dt/(1d/120)));for(int i=0;i<n;i++){Step(nod,150,15,dt/n);Step(lift,230,19,dt/n);}
            browAge+=dt;double a=act*amount;bool talking=quiet<.35;double back=Math.Min(1,dt*(talking?3:.45));
            dir.x+=((talking?aim.x:0)-dir.x)*back;dir.y+=((talking?aim.y:0)-dir.y)*back;
            double driftX=(Math.Sin(t*.9+phase[0])*.6+Math.Sin(t*1.7+phase[1])*.4)*3.5*a;
            double driftZ=(Math.Sin(t*.7+phase[2])*.6+Math.Sin(t*1.3+phase[0])*.4)*2.5*a;
            off["angleX"]=dir.x*amount+driftX;off["angleY"]=nod.x+dir.y*amount;off["angleZ"]=driftZ;off["bodyX"]=off["angleX"]*.15;
            off["browLY"]=off["browRY"]=Params.Smooth(0,.12,browAge)*(1-Params.Smooth(.3,.65,browAge))*.55*amount;
            off["eyeBallX"]=-p.eyeBallX*.7*act;off["eyeBallY"]=-p.eyeBallY*.7*act;
            double cap=.02*amount;hop=bounce?Math.Min(cap,Math.Max(0,lift.x))-cap:0;return off;
        }
        public void Reset(){act=hop=valley=peak=0;armed=true;quiet=browAge=10;nod=new Spring();lift=new Spring();dir=new IdlePoint();aim=new IdlePoint();}
    }
}
