using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class LoadingTests
    {
        private static JObject Fixture()
        {
            var data=JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/loading.json")));
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var entry in ((JObject)data["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,entry.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)entry.Value),entry.Name);
            return data;
        }
        [Test] public async Task ProgressMatchesSource()
        {
            foreach(var scenario in Fixture()["progress"])
            {
                using(var loads=new LoadCoordinator())
                {
                    var actual=new JArray();
                    await loads.Start(new LoadOptions{onProgress=p=>actual.Add(p.step=="build"?new JObject{["step"]=p.step}:new JObject{["step"]=p.step,["progress"]=p.progress})},job=>
                    {foreach(var c in scenario["calls"])if(c.Type==JTokenType.String)job.Build();else job.Progress((double)c);return Task.FromResult(1);});
                    Assert.That(actual.Count,Is.EqualTo(scenario["expected"].Count()));
                    for(int i=0;i<actual.Count;i++){Assert.That((string)actual[i]["step"],Is.EqualTo((string)scenario["expected"][i]["step"]));if((string)actual[i]["step"]=="download")Assert.That((double)actual[i]["progress"],Is.EqualTo((double)scenario["expected"][i]["progress"]));}
                }
            }
        }
        [Test] public void FallbackVersionMatchesSourceModels()
        {
            foreach(var c in Fixture()["versions"])
            {
                var model=ModelPackage.FromDirectory(Path.Combine(ParityTests.RepoRoot,"characters",(string)c["name"])).model;
                Assert.That(ModelDownload.VersionTag(model["layers"],(string)c["lastModified"]),Is.EqualTo((string)c["expected"]),(string)c["name"]);
            }
        }
        [Test] public async Task PrecancelDoesNotReplacePendingLoad()
        {
            using(var loads=new LoadCoordinator())using(var stop=new CancellationTokenSource())
            {
                var ready=new TaskCompletionSource<int>();LoadJob first=null;
                var a=loads.Start<int>(null,j=>{first=j;return ready.Task;});stop.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async()=>await loads.Start(new LoadOptions{cancellation=stop.Token},j=>Task.FromResult(2)));
                Assert.That(first.Cancellation.IsCancellationRequested,Is.False);ready.SetResult(1);Assert.That(await a,Is.EqualTo(1));
            }
        }
        [Test] public void OldFailureCannotClearNewLoadAndCancellationWinsOverItsError()
        {
            using(var loads=new LoadCoordinator())
            {
                var old=new TaskCompletionSource<int>();var latest=new TaskCompletionSource<int>();LoadJob current=null;
                var a=loads.Start<int>(null,j=>old.Task);var b=loads.Start<int>(null,j=>{current=j;return latest.Task;});
                old.SetException(new IOException("dropped"));Assert.CatchAsync<OperationCanceledException>(async()=>await a);
                loads.Dispose();Assert.That(current.Cancellation.IsCancellationRequested,Is.True);
                latest.SetException(new IOException("dropped"));Assert.CatchAsync<OperationCanceledException>(async()=>await b);
            }
        }
        [Test] public async Task SuccessAfterCommitBelongsToOriginalLoad()
        {
            using(var loads=new LoadCoordinator())
            {
                var committed=new TaskCompletionSource<int>();var a=loads.Start<int>(null,j=>committed.Task);
                Assert.That(await loads.Start(null,j=>Task.FromResult(2)),Is.EqualTo(2));
                committed.SetResult(1);Assert.That(await a,Is.EqualTo(1));
            }
        }
        [Test] public async Task CallbackExceptionIsReportedWithoutBreakingLoad()
        {
            using(var loads=new LoadCoordinator())
            {
                int errors=0;loads.OnCallbackError=e=>errors++;
                Assert.That(await loads.Start(new LoadOptions{onProgress=p=>throw new Exception("callback")},j=>{j.Progress(0);j.Build();return Task.FromResult(7);}),Is.EqualTo(7));
                Assert.That(errors,Is.EqualTo(3));
            }
        }
        [Test] public void DisposeRejectsBeforeStartingWork()
        {
            var loads=new LoadCoordinator();loads.Dispose();bool invoked=false;
            Assert.CatchAsync<OperationCanceledException>(async()=>await loads.Start(null,j=>{invoked=true;return Task.FromResult(0);}));Assert.That(invoked,Is.False);
        }
        [Test] public async Task ReentrantProgressReplacementCancelsBeforeCommit()
        {
            using(var loads=new LoadCoordinator())
            {
                Task<int> replacement=null;
                var a=loads.Start(new LoadOptions{onProgress=p=>replacement=loads.Start(null,j=>Task.FromResult(2))},j=>{j.Build();j.Cancellation.ThrowIfCancellationRequested();return Task.FromResult(1);});
                Assert.CatchAsync<OperationCanceledException>(async()=>await a);Assert.That(await replacement,Is.EqualTo(2));
            }
        }
        private sealed class FakeTransport : IModelTransport
        {
            public readonly List<string> urls=new List<string>();
            public Func<Uri,int,bool,CancellationToken,Task<ModelResponse>> get;
            public Task<ModelResponse> Get(Uri u,int cap,bool fresh,CancellationToken token,Action<double> progress=null){urls.Add(u.AbsoluteUri);return get(u,cap,fresh,token);}
        }
        private static Task<ModelResponse> Response(byte[] b)=>Task.FromResult(new ModelResponse{bytes=b});
        [TestCase(503,2),TestCase(404,1),TestCase(0,2)]
        public void PackageRetriesOnlyNetworkOrServerErrors(int status,int attempts)
        {
            using(var loads=new LoadCoordinator())
            {
                var http=new FakeTransport{get=(u,c,n,t)=>throw new LivLivError(status==0?"unreachable":"http","failed",status:status==0?(int?)null:status)};
                Assert.ThrowsAsync<LivLivError>(async()=>await loads.Start(null,j=>ModelDownload.Load(new Uri("https://example.test/char.LIVLIV?key=demo"),j,http)));
                Assert.That(http.urls.Count,Is.EqualTo(attempts));
            }
        }
        [Test] public void NoRetryForDownloadBudgetFailure()
        {
            using(var loads=new LoadCoordinator())
            {
                var http=new FakeTransport{get=(u,c,n,t)=>throw new LivLivError("too-large","failed")};
                Assert.ThrowsAsync<LivLivError>(async()=>await loads.Start(null,j=>ModelDownload.Load(new Uri("https://example.test/char.zip"),j,http)));Assert.That(http.urls.Count,Is.EqualTo(1));
            }
        }
        [Test] public async Task FolderLoadsImagesVersionsAndOptionalKeyforms()
        {
            var raw=JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"characters/d01/model.json")));
            raw=(JObject)LivLiv.Runtime.ModelFormat.ReadModel(raw).DeepClone();raw["format"]="livemaker-puppet";raw["version"]=3;raw["tex"]="build-tag";raw["keyforms"]="missing.json";byte[] pixels={1,2,3};
            var progress=new List<string>();
            var http=new FakeTransport{get=(u,cap,fresh,token)=>
            {
                if(u.AbsolutePath.EndsWith("/model.json")){Assert.That(fresh,Is.True);return Response(Encoding.UTF8.GetBytes(raw.ToString()));}
                Assert.That(u.Query,Is.EqualTo("?v=build-tag&key=demo"));
                if(u.AbsolutePath.EndsWith("missing.json"))throw new LivLivError("http","404",404);
                return Response(pixels);
            }};
            using(var loads=new LoadCoordinator())
            {
                var package=await loads.Start(new LoadOptions{onProgress=p=>progress.Add(p.step)},j=>ModelDownload.Load(new Uri("https://example.test/character?key=demo"),j,http));
                Assert.That(http.urls[0],Is.EqualTo("https://example.test/character/model.json?key=demo"));
                Assert.That(package.warnings.Count,Is.EqualTo(1));Assert.That(progress.Last(),Is.EqualTo("build"));
                var layer=(JObject)package.model["layers"][0];Assert.That(package.ReadImage(layer,(string)layer["texture"]),Is.SameAs(pixels));
                Assert.That(http.urls.Any(u=>u.Contains("textures.bin")),Is.False);
            }
        }
    }
}
