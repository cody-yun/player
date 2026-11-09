// Port of src/base.ts frame composition, controls and driver ownership. Copyright 2026 LivLiv. Apache-2.0.
// Unity host calls Tick with elapsed seconds and supplies pointer/display events. Async loading is a separate adapter.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LivLiv.Runtime;
namespace LivLiv
{
    public sealed class PlayerOptions
    {
        public bool idle=true,autoplay=true,reducedMotion;
        public string followPointer="page",lang="en";
        public double maxPixelRatio=2;
    }
    public sealed class PlayerView { public double x,y,zoom=1;public bool head; }
    public interface IPlayerHost { Action Use(IPlayerDriver driver);void Blink(); }
    public interface IPlayerDriver : IDisposable { int Order { get; } void Update(PlayerFrame frame,IPlayerHost player); }
    public abstract class PlayerDriver : IPlayerDriver
    {
        public virtual int Order=>0;
        public abstract void Update(PlayerFrame frame,IPlayerHost player);
        public virtual void Dispose(){}
    }
    public sealed class PlayerFrame
    {
        public readonly Params @params;
        public double dt { get; internal set; }
        public double time { get; internal set; }
        public bool tracked;
        public readonly ViewTransform move=new ViewTransform();
        internal PlayerFrame(Params pose){@params=pose;}
        public void Add(IReadOnlyDictionary<string,double> offsets)
        {
            foreach(var offset in offsets)if(offset.Value!=0&&!double.IsNaN(offset.Value))@params[offset.Key]=Params.Clamp(@params[offset.Key]+offset.Value,Params.Ranges[offset.Key][0],Params.Ranges[offset.Key][1]);
        }
    }
    public abstract class PlayerBase : IPlayerHost,IDisposable
    {
        public readonly Params @params=new Params();
        public readonly PlayerView view=new PlayerView();
        public bool idle,visible=true;
        public string followPointer;
        public readonly string lang;
        public bool Playing { get; private set; }
        public bool IsDisposed=>disposed;
        public string Poster { get; private set; }
        public event Action<string> PosterChanged;
        protected bool disposed;
        protected readonly ActionPlayer actions=new ActionPlayer();
        protected readonly PlayerFrame frame;
        protected readonly LoadCoordinator loads=new LoadCoordinator();
        protected Task<T> StartLoad<T>(LoadOptions options,Func<LoadJob,Task<T>> load)
        {
            if(ContextLost)return Task.FromException<T>(new LivLivError("context-lost","LivLiv: the rendering context was lost"));
            return loads.Start(options,load);
        }
        private readonly Idle idler;
        private readonly List<IPlayerDriver> drivers=new List<IPlayerDriver>();
        private readonly Dictionary<string,double> expr=new Dictionary<string,double>();
        private double smile,wasMouth;
        private bool wasTracked;
        private static readonly Params Defaults=new Params();
        protected PlayerBase(Params pose,PlayerOptions options=null,Func<double> random=null)
        {
            options=options??new PlayerOptions();idle=options.idle;followPointer=options.followPointer;lang=options.lang;frame=new PlayerFrame(pose);idler=new Idle(random,options.reducedMotion);Playing=options.autoplay;loads.OnPoster=SetPoster;
        }
        protected abstract bool Drawable { get; }
        protected virtual bool HasContent=>Drawable;
        protected void ClearPoster()=>SetPoster(null);
        // src/base.ts setPoster: CSS is replaced by a surface notification, never by rendered model pixels.
        private void SetPoster(string url)
        {
            string next=string.IsNullOrEmpty(url)||HasContent?null:url;
            if(Poster==next)return;Poster=next;
            var handlers=PosterChanged;
            if(handlers!=null)foreach(Action<string> handler in handlers.GetInvocationList())
                {if(Poster!=next)break;try{handler(next);}catch(Exception e){loads.Report(e);}}
        }
        protected abstract double Clock { get; }
        protected abstract IDictionary<string,double> ShownExpressions { get; }
        protected abstract IEnumerable<string> ExpressionIds { get; }
        protected abstract Puppet Seen { get; }
        protected abstract double DisplayWidth { get; }
        protected abstract double DisplayHeight { get; }
        private bool contextLost;
        public virtual bool ContextLost=>contextLost;
        public Action OnContextLost;
        // Platform hosts forward a confirmed graphics-context loss on the main thread.
        public void NotifyContextLost()
        {
            if(disposed||contextLost)return;
            contextLost=true;loads.Abort(new LivLivError("context-lost","LivLiv: the rendering context was lost"));
            OnContextLost?.Invoke();
        }
        protected abstract void Show(double dt);
        protected abstract void Free();
        protected void Neutral(){expr.Clear();smile=0;}
        public bool SetExpression(string id,double weight=1)
        {
            if(id!=null&&!ExpressionIds.Contains(id))return false;double w=Params.Clamp(weight,0,1);smile=id=="smile"?w:0;expr.Clear();if(id!=null&&id!="smile")expr[id]=w;return true;
        }
        public void Act(string action){if(action=="away")throw new ArgumentException("LivLiv: act('away') is setAway(true) now",nameof(action));actions.Play(action);}
        public void SetAway(bool on)=>actions.SetAway(on);
        public void LookAt(double? x,double y=0)=>idler.LookAt(Seen,x==null?(double?)null:Params.Clamp(x.Value,-1,1),Params.Clamp(y,-1,1),DisplayWidth,DisplayHeight);
        public void Blink()=>idler.Blink(Clock);
        public Action Use(IPlayerDriver driver)
        {
            if(driver==null)throw new ArgumentNullException(nameof(driver));if(disposed){driver.Dispose();return ()=>{};}
            drivers.Add(driver);
            // LINQ OrderBy is stable, like ECMAScript Array.sort (drivers with equal order keep their insertion order).
            var ordered=drivers.OrderBy(d=>d.Order).ToArray();drivers.Clear();drivers.AddRange(ordered);
            return ()=>{int index=drivers.IndexOf(driver);if(index>=0){drivers.RemoveAt(index);driver.Dispose();}};
        }
        public void Play(){if(!disposed)Playing=true;}
        public void Pause(){Playing=false;}
        public void Tick(double elapsedSeconds){if(Playing&&visible)Draw(Math.Min(.05,elapsedSeconds));}
        public void Resize()=>Draw(0);
        protected void Draw(double dt)
        {
            if(!Drawable||disposed||ContextLost)return;
            var p=frame.@params;double time=Clock;var shown=ShownExpressions;
            idler.Shown(p);foreach(var key in Params.Ranges.Keys)p[key]=@params[key];
            if(idle)idler.Apply(p,time,dt,actions.Busy||wasMouth>.05,wasTracked,smile==0&&!shown.Values.Any(v=>v>.02));
            frame.dt=dt;frame.time=time;frame.tracked=false;frame.move.x=frame.move.y=0;frame.move.s=1;
            // Index iteration retains source semantics if a driver removes itself during update.
            for(int i=0;i<drivers.Count;i++)drivers[i].Update(frame,this);
            wasTracked=frame.tracked;wasMouth=p.mouthOpen;frame.Add(actions.Update(dt));if(smile!=0)p.smile=Math.Max(p.smile,smile);
            if(actions.awayW!=0)foreach(var offset in actions.AwayPose(time))p[offset.Key]+=(offset.Value-p[offset.Key])*actions.awayW;
            foreach(var key in Params.Ranges.Keys){double v=p[key];p[key]=double.IsNaN(v)?Defaults[key]:Params.Clamp(v,Params.Ranges[key][0],Params.Ranges[key][1]);}
            foreach(var key in expr.Keys)if(!shown.ContainsKey(key))shown[key]=0;
            foreach(var key in shown.Keys.ToArray()){double target=expr.TryGetValue(key,out var value)?value:0;shown[key]+=(target*(1-actions.awayW)-shown[key])*Math.Min(1,dt*8);}
            Show(dt);
            if(Poster!=null)SetPoster(null);
        }
        // Coordinates are relative to the visible player rectangle, with origin at its top-left.
        public void PointerMove(double x,double y)
        {
            if(string.IsNullOrEmpty(followPointer))return;bool inside=!(x<0||x>DisplayWidth||y<0||y>DisplayHeight);
            idler.Pointer(Seen,x,y,DisplayWidth,DisplayHeight,followPointer=="page"||inside);
        }
        public void PointerLeave()=>idler.Leave();
        public void Dispose()
        {
            if(disposed)return;Pause();disposed=true;loads.Dispose();SetPoster(null);PosterChanged=null;var owned=drivers.ToArray();drivers.Clear();foreach(var driver in owned)driver.Dispose();Free();
        }
    }
}
