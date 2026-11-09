// RenderTexture boundary for src/release.ts. Never loses Unity's shared graphics context.
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;
namespace LivLiv.Unity
{
    public sealed class PlayerDisposeOptions
    {
        // Explicitly release the current target's GPU storage after this main-thread task.
        // The RenderTexture object remains caller-owned, but cannot host another Player after release.
        public bool release;
    }
    internal static class RenderTargetRelease
    {
        private sealed class Owners { public readonly List<WeakReference<Player>> players=new List<WeakReference<Player>>(); }
        private static readonly ConditionalWeakTable<RenderTexture,Owners> owners=new ConditionalWeakTable<RenderTexture,Owners>();
        private static readonly CanvasRelease registry=new CanvasRelease(action=>
        {
            var context=SynchronizationContext.Current;
            if(context==null)throw new LivLivError("missing-feature","Deferred target release requires Unity's main-thread SynchronizationContext");
            context.Post(_=>action(),null);
        });
        public static void Claim(RenderTexture target)
        {
            try{registry.ClaimCanvas(target);}
            catch(LivLivError error)when(error.code=="released")
            {throw new LivLivError("released","LivLiv: this RenderTexture gave up its GPU storage; use a new RenderTexture",cause:error);}
        }
        public static Action Observe(RenderTexture target,Player player)
        {
            var list=owners.GetValue(target,_=>new Owners());list.players.RemoveAll(item=>!item.TryGetTarget(out _));
            var weak=new WeakReference<Player>(player);list.players.Add(weak);return ()=>list.players.Remove(weak);
        }
        public static void Release(RenderTexture target)=>registry.ReleaseCanvas(target,()=>
        {
            if(target==null)return;
            if(RenderTexture.active==target)RenderTexture.active=null;
            target.Release();
            // Notify existing owners even when paused, like the source canvas loss event.
            // Other targets retain Unity's shared graphics context.
            if(owners.TryGetValue(target,out var list))foreach(var weak in list.players.ToArray())
                if(weak.TryGetTarget(out var player)&&!player.IsDisposed&&ReferenceEquals(player.Target,target))
                    try{player.NotifyContextLost();}catch(Exception error){Debug.LogException(error);}
        });
    }
    public sealed partial class Player
    {
        public void Dispose(PlayerDisposeOptions options)
        {
            var target=Target; // A disposing driver may retarget its host; release the surface selected at call time.
            base.Dispose();
            if(options?.release==true&&target!=null)RenderTargetRelease.Release(target);
        }
    }
}
