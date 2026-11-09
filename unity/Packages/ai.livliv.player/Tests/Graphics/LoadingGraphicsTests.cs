using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace LivLiv.Tests
{
    public sealed class LoadingGraphicsTests
    {
        // Test-only loopback HTTP origin; no external server, camera, or microphone.
        private sealed class Origin : IDisposable
        {
            private readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
            private readonly CancellationTokenSource stop=new CancellationTokenSource();
            private readonly List<TcpClient> clients=new List<TcpClient>();
            private readonly byte[] zip;private readonly string modelRoot;
            public readonly string Url;public int retries;
            public Origin()
            {
                modelRoot=Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters/char06"));
                using(var data=new MemoryStream())
                {
                    using(var z=new ZipArchive(data,ZipArchiveMode.Create,true))foreach(var p in Directory.GetFiles(modelRoot,"*",SearchOption.AllDirectories))
                        using(var entry=z.CreateEntry(p.Substring(modelRoot.Length+1).Replace('\\','/')).Open()){var bytes=File.ReadAllBytes(p);entry.Write(bytes,0,bytes.Length);}
                    zip=data.ToArray();
                }
                listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;_ = Serve();
            }
            private async Task Serve()
            {
                try{while(!stop.IsCancellationRequested){var c=await listener.AcceptTcpClientAsync();lock(clients)clients.Add(c);_ = Reply(c);}}
                catch(Exception) when(stop.IsCancellationRequested){}
            }
            private async Task Reply(TcpClient client)
            {
                try
                {
                    using(client)using(var stream=client.GetStream())using(var reader=new StreamReader(stream,Encoding.ASCII,false,4096,true))
                    {
                        string first=await reader.ReadLineAsync();if(first==null)return;string line;while(!string.IsNullOrEmpty(line=await reader.ReadLineAsync())){}
                        string path=first.Split(' ')[1].Split('?')[0];byte[] body=zip;int status=200;
                        if(path.StartsWith("/folder/",StringComparison.Ordinal))body=File.ReadAllBytes(Path.Combine(modelRoot,path.Substring(8)));
                        if(path=="/retry.zip"&&Interlocked.Increment(ref retries)==1)status=503;
                        if(path=="/missing.zip")status=404;if(path=="/bad.zip")body=new byte[]{0,1,2};
                        if(status!=200)body=Array.Empty<byte>();
                        var header=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+" Result\r\nContent-Length: "+body.Length+"\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header,0,header.Length);
                        for(int at=0;at<body.Length;at+=16384){await stream.WriteAsync(body,at,Math.Min(16384,body.Length-at));if(path=="/slow.zip")await Task.Delay(25,stop.Token);}
                    }
                }
                catch(Exception e) when(e is IOException||e is ObjectDisposedException||e is OperationCanceledException){}
                finally{lock(clients)clients.Remove(client);}
            }
            public void Dispose(){stop.Cancel();listener.Stop();lock(clients)foreach(var client in clients)client.Close();}
        }
        [UnityTest] public IEnumerator RealHttpLoadsRetriesCancelsAndPreservesRenderer()
        {
            var task=Exercise();while(!task.IsCompleted)yield return null;
            if(task.IsFaulted)throw task.Exception.InnerException;
            Assert.That(task.IsCanceled,Is.False);
        }
        private static async Task Exercise()
        {
            using(var origin=new Origin())using(var player=new Player(null,new PlayerOptions{autoplay=false,idle=false}))
            {
                var events=new List<LoadProgress>();
                var options=new LoadOptions{onProgress=p=>events.Add(p)};
                var first=await player.LoadAsync(origin.Url+"/package.zip",options);Assert.That(first.name,Is.Not.Empty);
                Assert.That(events[0].step,Is.EqualTo("download"));Assert.That(events[events.Count-1].step,Is.EqualTo("build"));
                var model=player.Renderer.Model;
                try{await player.LoadAsync(origin.Url+"/missing.zip");Assert.Fail("404 accepted");}catch(LivLivError e){Assert.That(e.code,Is.EqualTo("http"));Assert.That(e.status,Is.EqualTo(404));}
                Assert.That(player.Renderer.Model,Is.SameAs(model));Assert.That(player.Info,Is.SameAs(first));
                try{await player.LoadAsync(origin.Url+"/bad.zip");Assert.Fail("invalid ZIP accepted");}catch(LivLivError e){Assert.That(e.code,Is.EqualTo("not-a-model"));}
                Assert.That(player.Renderer.Model,Is.SameAs(model));
                await player.LoadAsync(origin.Url+"/retry.zip");Assert.That(origin.retries,Is.EqualTo(2));
                using(var cancel=new CancellationTokenSource())
                {
                    var slow=player.LoadAsync(origin.Url+"/slow.zip",new LoadOptions{cancellation=cancel.Token,onProgress=p=>{if(p.step=="download"&&p.progress>0)cancel.Cancel();}});
                    try{await slow;Assert.Fail("cancel accepted");}catch(OperationCanceledException){}
                }
                var replaced=player.LoadAsync(origin.Url+"/slow.zip");
                await player.LoadAsync(origin.Url+"/folder");
                try{await replaced;Assert.Fail("replaced load succeeded");}catch(OperationCanceledException){}
                Assert.That(player.Info.name,Is.EqualTo(first.name));
                try{await new UnityModelTransport().Get(new Uri(origin.Url+"/package.zip"),10,false,default);Assert.Fail("byte cap ignored");}catch(LivLivError e){Assert.That(e.code,Is.EqualTo("too-large"));}
                var disposed=player.LoadAsync(origin.Url+"/slow.zip");player.Dispose();
                try{await disposed;Assert.Fail("disposed load succeeded");}catch(OperationCanceledException){}
                Assert.That(player.Info,Is.Null);
            }
        }
        [UnityTest]public IEnumerator WorkerCancellationReturnsToUnityAndKeepsPreviousModel()
        {
            var task=WorkerCancellation();while(!task.IsCompleted)yield return null;
            if(task.IsFaulted)throw task.Exception.InnerException;
            Assert.That(task.IsCanceled,Is.False);
        }
        private static async Task WorkerCancellation()
        {
            int owner=Thread.CurrentThread.ManagedThreadId,worker=owner,started=0;
            using(var origin=new Origin())using(var player=new Player(null,new PlayerOptions{autoplay=false,idle=false}))
            using(var cancel=new CancellationTokenSource())
            {
                var first=await player.LoadAsync(origin.Url+"/package.zip");var model=player.Renderer.Model;
                var completed=new TaskCompletionSource<bool>();
                var slow=player.LoadAsync(origin.Url+"/slow.zip",new LoadOptions{cancellation=cancel.Token,onProgress=p=>
                {
                    if(p.step!="download"||p.progress<=0||Interlocked.Exchange(ref started,1)!=0)return;
                    _=Task.Run(()=>
                    {
                        try{worker=Thread.CurrentThread.ManagedThreadId;cancel.Cancel();completed.TrySetResult(true);}
                        catch(Exception error){completed.TrySetException(error);}
                    });
                }});
                try{await slow;Assert.Fail("Worker cancellation was ignored");}catch(OperationCanceledException){}
                await completed.Task;Assert.That(worker,Is.Not.EqualTo(owner));
                Assert.That(Thread.CurrentThread.ManagedThreadId,Is.EqualTo(owner));
                Assert.That(player.Info,Is.SameAs(first));Assert.That(player.Renderer.Model,Is.SameAs(model));
                await player.LoadAsync(origin.Url+"/package.zip");Assert.That(player.Info.name,Is.EqualTo(first.name));
            }
        }
    }
}
