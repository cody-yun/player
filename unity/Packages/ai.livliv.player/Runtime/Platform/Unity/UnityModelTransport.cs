// HTTP(S) adapter used on desktop, Android/iOS and WebGL. No threads, disk IO or blocking waits.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using UnityEngine.Networking;
namespace LivLiv.Unity
{
    public sealed class UnityModelTransport : IModelTransport
    {
        private static async Task NextFrame(){await Task.Yield();}
        private sealed class Body : DownloadHandlerScript
        {
            private readonly MemoryStream stream=new MemoryStream();
            private readonly int cap;
            public bool TooLarge { get; private set; }
            public long Length=>stream.Length;
            public Body(int cap):base(new byte[64*1024]){this.cap=cap;}
            protected override bool ReceiveData(byte[] data,int count)
            {
                if(count>cap-stream.Length){TooLarge=true;return false;}
                if(count>0)stream.Write(data,0,count);return true;
            }
            protected override byte[] GetData()=>stream.ToArray();
            public override void Dispose(){stream.Dispose();base.Dispose();}
        }
        public async Task<ModelResponse> Get(Uri url,int maxBytes,bool noCache,CancellationToken cancellation,Action<double> progress=null)
        {
            cancellation.ThrowIfCancellationRequested();
            using(var body=new Body(maxBytes))
            using(var request=new UnityWebRequest(url.AbsoluteUri,UnityWebRequest.kHttpVerbGET,body,null))
            {
                request.disposeDownloadHandlerOnDispose=false;request.timeout=60;
                if(noCache)request.SetRequestHeader("Cache-Control","no-cache");
                var operation=request.SendWebRequest();
                var cancelled=new TaskCompletionSource<int>();
                int ownerThread=Thread.CurrentThread.ManagedThreadId;
                using(var registration=cancellation.Register(()=>
                {
                    // Worker-thread cancellation wakes the captured Unity context; only the owner
                    // thread may directly touch UnityWebRequest. Fatal browser loss runs on that thread.
                    try{if(Thread.CurrentThread.ManagedThreadId==ownerThread)request.Abort();}
                    finally{cancelled.TrySetResult(0);}
                }))
                try
                {
                    while(!operation.isDone)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if(string.IsNullOrEmpty(request.GetResponseHeader("Content-Encoding"))&&long.TryParse(request.GetResponseHeader("Content-Length"),out long size)&&size>0)
                            progress?.Invoke(Math.Min(1,(double)body.Length/size));
                        // Context loss can stop Unity frames. Cancellation must wake this wait directly.
                        await Task.WhenAny(NextFrame(),cancelled.Task);
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if(body.TooLarge)throw new LivLivError("too-large","LivLiv: download exceeds "+maxBytes+" bytes");
                    if(request.responseCode>=400)throw new LivLivError("http","LivLiv: HTTP "+request.responseCode,status:(int)request.responseCode);
                    if(request.result!=UnityWebRequest.Result.Success)throw new LivLivError("unreachable","LivLiv: model is unreachable or the connection dropped: "+request.error);
                    return new ModelResponse{bytes=body.data,lastModified=request.GetResponseHeader("Last-Modified")};
                }
                finally{if(!operation.isDone)request.Abort();}
            }
        }
    }
}
