// Port of src/follow.ts. Copyright 2026 LivLiv. Apache-2.0.
// Host supplies display size, pointer events and the reduced-motion preference.
using System;
using LivLiv.Runtime;
namespace LivLiv
{
    public sealed class Idle
    {
        public static readonly string[] IdleKeys={"angleX","angleY","angleZ","eyeLOpen","eyeROpen","eyeBallX","eyeBallY","browLY","browRY","bodyX","bodyZ","breath"};
        public readonly LivelyIdle lv;
        private readonly Params pose=new Params();
        private IdlePoint turn=new IdlePoint();
        private bool held;
        public Idle(Func<double> random=null,bool reducedMotion=false){lv=new LivelyIdle(random);if(reducedMotion)lv.moves=0;}
        public void Apply(Params output,double time,double dt,bool busy,bool tracked,bool faceFree)
        {
            lv.busy=busy;lv.tracked=tracked;lv.faceFree=faceFree;lv.look=turn;lv.Apply(pose,time,dt);
            foreach(var key in IdleKeys)output[key]=pose[key];output.smile=Math.Max(output.smile,pose.smile);
        }
        public void Shown(Params output){lv.shown.x=output.angleX;lv.shown.z=output.angleZ;}
        public void Pointer(Puppet puppet,double x,double y,double width,double height,bool on)
        {
            if(held)return;LivelyIdle.PointAt(lv,puppet,x,y,width,height,on);
            if(on)turn=new IdlePoint{x=Params.Clamp((x/Math.Max(1,width)-.5)*2,-1,1),y=Params.Clamp(-(y/Math.Max(1,height)-.3)*1.6,-1,1)};
        }
        public void Leave(){if(!held)lv.pointer.on=false;turn=new IdlePoint();}
        public void LookAt(Puppet puppet,double? x,double y,double width,double height)
        {
            held=false;
            if(x==null){lv.pointer.on=false;lv.pointer.keep=false;turn=new IdlePoint();return;}
            Pointer(puppet,(x.Value+1)/2*width,(1-y)/2*height,width,height,true);lv.pointer.keep=true;held=true;turn=new IdlePoint{x=x.Value,y=y};
        }
        public void Blink(double time){lv.nextBlink=Math.Min(lv.nextBlink,time);}
    }
}
