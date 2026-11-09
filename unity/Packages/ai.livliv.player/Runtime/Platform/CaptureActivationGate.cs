// Unity host lifecycle policy, not an upstream tracking parameter.
using System;
using System.Threading;
using System.Threading.Tasks;
namespace LivLiv.Platform
{
    // Main-thread use. Permission dialogs may pause the app; capture must wait for foreground.
    public sealed class CaptureActivationGate
    {
        private bool paused;
        private TaskCompletionSource<bool> foreground;
        public bool IsAuthorizing {get;private set;}
        public bool IsPaused=>paused;
        // True means a pending device start should be cancelled by the host.
        public bool SetPaused(bool value)
        {
            paused=value;
            if(!value){var signal=foreground;foreground=null;signal?.TrySetResult(true);}
            return value&&!IsAuthorizing;
        }
        public async Task AuthorizeAsync(Func<CancellationToken,Task> request,CancellationToken token)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            token.ThrowIfCancellationRequested();
            if(IsAuthorizing)throw new InvalidOperationException("Authorization already pending");
            IsAuthorizing=true;
            try
            {
                await request(token);
                token.ThrowIfCancellationRequested();
                while(paused)
                {
                    var signal=foreground??(foreground=new TaskCompletionSource<bool>());
                    using(token.Register(()=>signal.TrySetCanceled(token)))await signal.Task;
                    token.ThrowIfCancellationRequested();
                }
            }
            finally{IsAuthorizing=false;if(foreground?.Task.IsCompleted==true)foreground=null;}
        }
    }
}
