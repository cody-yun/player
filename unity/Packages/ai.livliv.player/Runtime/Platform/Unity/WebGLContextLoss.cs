using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
namespace LivLiv.Unity
{
    internal static class WebGLContextLoss
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void LostCallback(int id);
        private static readonly LostCallback callback=Lost;
        private sealed class Owner{public Player player;public SynchronizationContext context;}
        private static readonly Dictionary<int,Owner> players=new Dictionary<int,Owner>();
        private static int next;
        [DllImport("__Internal")]private static extern void LivLivContext_Register(int id,LostCallback callback);
        [DllImport("__Internal")]private static extern void LivLivContext_Unregister(int id);
        [AOT.MonoPInvokeCallback(typeof(LostCallback))]private static void Lost(int id)
        {
            if(!players.TryGetValue(id,out var owner))return;
            var previous=SynchronizationContext.Current;
            // Browser events enter WASM outside Unity's frame pump. Preserve the owner's main-thread
            // context so cancellation continuations can finish inline while that pump is stopped.
            try{SynchronizationContext.SetSynchronizationContext(owner.context);owner.player.NotifyContextLost();}
            catch(Exception error){Debug.LogException(error);}
            finally{SynchronizationContext.SetSynchronizationContext(previous);}
        }
#endif
        public static Action Subscribe(Player player)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            int id=++next;players.Add(id,new Owner{player=player,context=SynchronizationContext.Current});
            try{LivLivContext_Register(id,callback);}catch{players.Remove(id);throw;}
            return ()=>{if(players.Remove(id))LivLivContext_Unregister(id);};
#else
            return ()=>{};
#endif
        }
    }
}
