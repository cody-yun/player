// Source mapping: src/base.ts start/LoadJob. Copyright 2026 LivLiv. Apache-2.0.
// Browser AbortSignal becomes CancellationToken. Call owner methods on the Unity main thread.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace LivLiv
{
    public sealed class LoadOptions
    {
        public CancellationToken cancellation;
        public Action<LoadProgress> onProgress;
        // src/base.ts LoadOptions.poster: shown only until an empty surface draws its first frame.
        public string poster;
    }
    public readonly struct LoadProgress
    {
        public readonly string step;
        public readonly double progress;
        internal LoadProgress(string step,double progress=0){this.step=step;this.progress=progress;}
    }
    public sealed class LoadJob
    {
        public CancellationToken Cancellation { get; }
        private readonly Action<LoadProgress> tell;
        private readonly Action<Exception> report;
        private double shown=-1;
        private bool built;
        internal LoadJob(CancellationToken cancellation,Action<LoadProgress> tell,Action<Exception> report)
        {Cancellation=cancellation;this.tell=tell;this.report=report;}
        private void Tell(LoadProgress value)
        {
            try {tell?.Invoke(value);}
            catch(Exception e){try{report?.Invoke(e);}catch{/* A diagnostic callback cannot break a load. */}}
        }
        public void Progress(double share)
        {if(share>shown&&!built){shown=share;Tell(new LoadProgress("download",share));}}
        public void Build()
        {if(!built){if(shown>=0&&shown<1)Tell(new LoadProgress("download",1));built=true;Tell(new LoadProgress("build"));}}
    }
    // Kept independent of surfaces so the same race/progress contract is testable on CLR and IL2CPP.
    public sealed class LoadCoordinator : IDisposable
    {
        private sealed class Pending { public CancellationTokenSource source;public Exception reason;public Action<Exception> reject; }
        private Pending running;
        private bool disposed;
        public Action<Exception> OnCallbackError { get; set; }
        internal Action<string> OnPoster;
        internal void Report(Exception error){try{OnCallbackError?.Invoke(error);}catch{}}
        public Task<T> Start<T>(LoadOptions options,Func<LoadJob,Task<T>> load)
        {
            var result=new TaskCompletionSource<T>();
            _=Forward();return result.Task;
            async Task Forward()
            {
                try{result.TrySetResult(await Run(options,load,error=>result.TrySetException(error)));}
                catch(OperationCanceledException error){result.TrySetCanceled(error.CancellationToken);}
                catch(Exception error){result.TrySetException(error);}
            }
        }
        private async Task<T> Run<T>(LoadOptions options,Func<LoadJob,Task<T>> load,Action<Exception> reject)
        {
            options=options??new LoadOptions();
            options.cancellation.ThrowIfCancellationRequested(); // Do not replace an existing load.
            if(disposed)throw new OperationCanceledException("LivLiv: the player was disposed");
            Replace();
            var ctl=CancellationTokenSource.CreateLinkedTokenSource(options.cancellation);
            var pending=new Pending{source=ctl,reject=reject};running=pending;
            try
            {
                OnPoster?.Invoke(options.poster);
                ctl.Token.ThrowIfCancellationRequested(); // A host's poster notification may replace/dispose this load.
                return await load(new LoadJob(ctl.Token,options.onProgress,OnCallbackError));
            }
            catch
            {
                if(ReferenceEquals(running,pending))OnPoster?.Invoke(null);
                if(pending.reason!=null)throw pending.reason;
                ctl.Token.ThrowIfCancellationRequested();throw;
            }
            finally{if(ReferenceEquals(running,pending))running=null;ctl.Dispose();}
            // No cancellation check after success: a committed model belongs to this load.
        }
        public void Replace()=>Abort(null);
        internal void Abort(Exception reason)
        {
            var previous=running;if(reason==null)running=null;
            if(previous!=null)
            {
                previous.reason=previous.reason??reason;
                try{previous.source.Cancel();}
                finally
                {
                    if(previous.reason!=null)
                    {
                        if(ReferenceEquals(running,previous))OnPoster?.Invoke(null);
                        previous.reject(previous.reason);
                    }
                }
            }
        }
        public void Dispose(){if(disposed)return;disposed=true;Replace();}
    }
}
