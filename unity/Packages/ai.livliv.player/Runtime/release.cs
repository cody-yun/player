// Port of src/release.ts. Copyright 2026 LivLiv. Apache-2.0.
// A canvas is a logical surface identity; Unity supplies a RenderTexture and a deferred main-thread queue.
using System;
using System.Runtime.CompilerServices;
namespace LivLiv
{
    public sealed class CanvasRelease
    {
        private sealed class State { public bool spent;public object pending; }
        private readonly ConditionalWeakTable<object,State> states=new ConditionalWeakTable<object,State>();
        private readonly Action<Action> defer;
        public CanvasRelease(Action<Action> defer){this.defer=defer??throw new ArgumentNullException(nameof(defer));}
        // Like the source module, calls and callbacks belong to one owning thread.
        public void ReleaseCanvas(object canvas,Action loseContext)
        {
            if(loseContext==null)return;
            if(canvas==null)throw new ArgumentNullException(nameof(canvas));
            var state=states.GetValue(canvas,_=>new State());if(state.spent||state.pending!=null)return;
            var ticket=new object();state.pending=ticket;
            try
            {
                defer(()=>
                {
                    if(!ReferenceEquals(state.pending,ticket))return;
                    state.pending=null;state.spent=true;
                    try{loseContext();}finally{GC.KeepAlive(canvas);}
                });
            }
            catch{if(ReferenceEquals(state.pending,ticket))state.pending=null;throw;}
        }
        public void ClaimCanvas(object canvas)
        {
            if(canvas==null)throw new ArgumentNullException(nameof(canvas));
            var state=states.GetValue(canvas,_=>new State());state.pending=null;
            if(state.spent)throw new LivLivError("released","LivLiv: this canvas gave up its WebGL context with dispose({ release: true }): use a new canvas");
        }
    }
}
