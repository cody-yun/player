// Port of src/tracking.ts driver and controls. Copyright 2026 LivLiv. Apache-2.0.
// A platform starts its camera/inference source before attaching it. Time is monotonic milliseconds.
using System;
using LivLiv.Runtime;
using LivLiv.Platform;
namespace LivLiv
{
    public interface IFaceInput : IDisposable
    {
        bool Ready { get; }
        double Fps { get; }
        bool Dark { get; }
        // Null: no new inference. Empty FaceFrame: a completed inference with no face.
        FaceFrame Next(double nowMilliseconds);
    }
    public sealed class FaceTrackingOptions
    {
        public bool mirror=true;public double smoothing=.5,follow;
        // Opt-in, as upstream. A stable character/user id; null/empty never touches storage.
        public string remember;
        // Unity hosts pass UnityCalibrationStore.Instance; the core never references Unity assemblies.
        public ICalibrationStore calibrationStore;
    }
    public sealed class FaceTracking : IDisposable
    {
        private readonly FaceTrackingDriver driver;private Action remove;
        private FaceTracking(FaceTrackingDriver driver,Action remove){this.driver=driver;this.remove=remove;}
        public bool Face=>driver.Tracker.lostFor<FaceTracker.LostAfter;
        public double Fps=>driver.Input.Fps;
        public bool Dark=>driver.Input.Dark;
        public bool Mirror {get=>driver.Tracker.mirror;set=>driver.Tracker.SetMirror(value);}
        public void Calibrate()=>driver.Tracker.Calibrate();
        public void Stop(){var end=remove;remove=null;end?.Invoke();}
        public void Dispose()=>Stop();
        public static FaceTracking Attach(IPlayerHost player,IFaceInput input,Func<double> nowMilliseconds,FaceTrackingOptions options=null)
        {
            if(player==null)throw new ArgumentNullException(nameof(player));
            var driver=new FaceTrackingDriver(input,nowMilliseconds,options);return new FaceTracking(driver,player.Use(driver));
        }
    }
    public sealed class FaceTrackingDriver : PlayerDriver
    {
        public readonly FaceTracker Tracker;
        public readonly IFaceInput Input;
        private readonly Func<double> now;
        private readonly double follow;
        private readonly Params pose=new Params();
        private double weight;private bool disposed;
        public override int Order=>-10;
        public FaceTrackingDriver(IFaceInput input,Func<double> nowMilliseconds,FaceTrackingOptions options=null)
        {
            Input=input??throw new ArgumentNullException(nameof(input));now=nowMilliseconds??throw new ArgumentNullException(nameof(nowMilliseconds));options=options??new FaceTrackingOptions();
            ICalibrationStore storage=input is IFaceInputMode mode&&mode.IsReplay?null:options.calibrationStore;
            Tracker=new FaceTracker(storage){calibId=options.remember};
            Tracker.SetMirror(options.mirror);Tracker.RestoreCalibration();Tracker.SetSmoothing(options.smoothing);follow=Params.Clamp(options.follow,0,2);
        }
        public override void Update(PlayerFrame frame,IPlayerHost player)
        {
            if(disposed||!Input.Ready)return;Tracker.follow=follow;double time=now();bool seen=Tracker.Poll(Input.Next(time),time);Tracker.Apply(pose,frame.dt);
            weight=Params.Clamp(weight+(seen?frame.dt/.3:-frame.dt/.5),0,1);frame.tracked=true;double k=weight*weight*(3-2*weight);if(k==0)return;
            foreach(var key in Tracker.target.Keys)frame.@params[key]+=(pose[key]-frame.@params[key])*k;
            var d=Tracker.pos;frame.move.x+=Params.Clamp(d.x*.006*follow*k,-.15,.15);frame.move.y+=Params.Clamp(d.y*.006*follow*k,-.15,.15);
            frame.move.s*=1+Params.Clamp(d.z*.012*follow*k,-.15,.2);
        }
        public override void Dispose(){if(disposed)return;disposed=true;Input.Dispose();}
    }
}
