using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class PosterTests
    {
        private sealed class Host:PlayerBase
        {
            public bool ready;public int draws,errors;
            private readonly Dictionary<string,double> shown=new Dictionary<string,double>();
            public Host():base(new Params(),new PlayerOptions{idle=false,autoplay=false}){loads.OnCallbackError=e=>errors++;}
            public Task<int> Load(LoadOptions options,Func<LoadJob,Task<int>> job)=>StartLoad(options,job);
            protected override bool Drawable=>ready;
            protected override double Clock=>0;
            protected override IDictionary<string,double> ShownExpressions=>shown;
            protected override IEnumerable<string> ExpressionIds=>Enumerable.Empty<string>();
            protected override Puppet Seen=>null;
            protected override double DisplayWidth=>100;
            protected override double DisplayHeight=>100;
            protected override void Show(double dt){draws++;}
            protected override void Free(){}
        }
        private static JObject Fixture()
        {
            var data=JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/poster.json")));
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var p in ((JObject)data["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value));
            return data;
        }
        public static IEnumerable<TestCaseData> Cases()=>Fixture()["scenarios"].Select(c=>new TestCaseData(c).SetName("Poster "+(string)c["name"]));
        private sealed class Pending
        {
            public CancellationTokenSource stop=new CancellationTokenSource();public TaskCompletionSource<int> result=new TaskCompletionSource<int>();public Task<int> task;
        }
        [TestCaseSource(nameof(Cases))]
        public async Task MatchesUpstream(JToken scenario)
        {
            using(var host=new Host())
            {
                var pending=new Dictionary<string,Pending>();int index=0;
                try
                {
                    foreach(var step in scenario["steps"])
                    {
                        string op=(string)step[0],id=step.Count()>1?(string)step[1]:null;
                        if(op=="start"||op=="precancel")
                        {
                            var job=new Pending();pending[id]=job;if(op=="precancel")job.stop.Cancel();
                            job.task=host.Load(new LoadOptions{poster=(string)step[2],cancellation=job.stop.Token},j=>job.result.Task);
                            if(op=="precancel")Assert.CatchAsync<OperationCanceledException>(async()=>await job.task);
                        }
                        else if(op=="resolve")
                        {if(step.Count()<3||step[2].Type!=JTokenType.Boolean|| (bool)step[2])host.ready=true;pending[id].result.SetResult(1);await pending[id].task;}
                        else if(op=="reject")
                        {pending[id].result.SetException(new IOException("failed"));Assert.CatchAsync<Exception>(async()=>await pending[id].task);}
                        else if(op=="cancel")pending[id].stop.Cancel();else if(op=="draw")host.Resize();else host.Dispose();
                        var expected=scenario["expected"][index++];
                        Assert.That(host.Poster,Is.EqualTo((string)expected["poster"]),op);Assert.That(host.draws,Is.EqualTo((int)expected["draws"]));
                    }
                }
                finally{foreach(var job in pending.Values)job.stop.Dispose();}
            }
        }
        [Test] public async Task ThrowingPosterObserverCannotBreakModelLoadOrOtherObservers()
        {
            using(var host=new Host())
            {
                int notifications=0;host.PosterChanged+=s=>throw new Exception("observer");host.PosterChanged+=s=>notifications++;
                Assert.That(await host.Load(new LoadOptions{poster="cover.webp"},j=>Task.FromResult(1)),Is.EqualTo(1));
                Assert.That(host.errors,Is.EqualTo(1));Assert.That(notifications,Is.EqualTo(1));host.Dispose();
                Assert.That(host.errors,Is.EqualTo(2));Assert.That(notifications,Is.EqualTo(2));
            }
        }
        [Test] public async Task ReentrantPosterReplacementDoesNotStartCancelledWorkOrPublishStaleState()
        {
            using(var host=new Host())
            {
                bool oldStarted=false;Task<int> replacement=null;var observed=new List<string>();
                host.PosterChanged+=s=>{if(s=="a")replacement=host.Load(new LoadOptions{poster="b"},j=>Task.FromResult(2));};
                host.PosterChanged+=observed.Add;
                var first=host.Load(new LoadOptions{poster="a"},j=>{oldStarted=true;return Task.FromResult(1);});
                Assert.CatchAsync<OperationCanceledException>(async()=>await first);Assert.That(await replacement,Is.EqualTo(2));
                Assert.That(oldStarted,Is.False);Assert.That(host.Poster,Is.EqualTo("b"));Assert.That(observed,Is.EqualTo(new[]{"b"}));
            }
        }
    }
}
