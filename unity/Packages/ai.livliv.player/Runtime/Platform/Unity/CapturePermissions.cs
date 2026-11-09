using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace LivLiv.Unity
{
    // Main-thread permission preflight, separate from opening a device. Also used by standalone adapters.
    public static class CapturePermissions
    {
        public static async Task RequestAsync(bool camera,CancellationToken token=default)
        {
            token.ThrowIfCancellationRequested();
#if UNITY_ANDROID && !UNITY_EDITOR
            string permission=camera?UnityEngine.Android.Permission.Camera:UnityEngine.Android.Permission.Microphone;
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                var result=new TaskCompletionSource<bool>();var callbacks=new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted+=_=>result.TrySetResult(true);
                callbacks.PermissionDenied+=_=>result.TrySetResult(false);
                callbacks.PermissionRequestDismissed+=_=>result.TrySetResult(false);
                UnityEngine.Android.Permission.RequestUserPermission(permission,callbacks);
                using(token.Register(()=>result.TrySetCanceled(token)))
                    if(!await result.Task)throw Denied(camera);
            }
#elif !UNITY_WEBGL || UNITY_EDITOR
            // Unity microphone authorization is supported on iOS. Desktop camera uses WebCam authorization.
#if UNITY_IOS && !UNITY_EDITOR
            bool requestPermission=true;
#else
            bool requestPermission=camera;
#endif
            var permission=camera?UserAuthorization.WebCam:UserAuthorization.Microphone;
            if(requestPermission&&!Application.HasUserAuthorization(permission))
            {
                var request=Application.RequestUserAuthorization(permission);
                while(!request.isDone){token.ThrowIfCancellationRequested();await Task.Yield();}
                if(!Application.HasUserAuthorization(permission))throw Denied(camera);
            }
#else
            // Browser bridges own getUserMedia; preserve their user-gesture activation path.
            await Task.CompletedTask;
#endif
            token.ThrowIfCancellationRequested();
        }
        private static UnauthorizedAccessException Denied(bool camera)=>new UnauthorizedAccessException("LivLiv: "+(camera?"camera":"microphone")+" permission was denied");
    }
}
