using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Runtime;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class ContextLossTests
    {
        private sealed class Host:PlayerBase
        {
            public int draws,frees;private readonly Dictionary<string,double> expressions=new Dictionary<string,double>();
            public Host():base(new Params(),new PlayerOptions{idle=false}){}
            public Task<int> Begin()=>StartLoad(new LoadOptions{poster="cover.png"},async job=>{await Task.Delay(Timeout.Infinite,job.Cancellation);return 1;});
            public Task<int> Held(Task<int> pending)=>StartLoad(new LoadOptions{poster="held.png"},job=>pending);
            protected override bool Drawable=>true;protected override bool HasContent=>false;
            protected override double Clock=>0;protected override double DisplayWidth=>1;protected override double DisplayHeight=>1;
            protected override IDictionary<string,double> ShownExpressions=>expressions;
            protected override IEnumerable<string> ExpressionIds=>Array.Empty<string>();protected override Puppet Seen=>null;
            protected override void Show(double dt){draws++;}protected override void Free(){frees++;}
        }
        private sealed class Driver:PlayerDriver
        {public int released;public override void Update(PlayerFrame f,IPlayerHost p){}public override void Dispose(){released++;}}
        [Test]public async Task LossAbortsPendingLoadNotifiesOnceAndStopsDrawsUntilDisposed()
        {
            var host=new Host();var driver=new Driver();host.Use(driver);host.Resize();int before=host.draws;
            int calls=0;host.OnContextLost=()=>{Assert.That(host.ContextLost,Is.True);calls++;host.NotifyContextLost();};
            var load=host.Begin();Assert.That(host.Poster,Is.EqualTo("cover.png"));host.NotifyContextLost();host.NotifyContextLost();
            var error=Assert.ThrowsAsync<LivLivError>(async()=>await load);Assert.That(error.code,Is.EqualTo("context-lost"));
            Assert.That(host.Poster,Is.Null);Assert.That(calls,Is.EqualTo(1));host.Tick(.016);host.Resize();Assert.That(host.draws,Is.EqualTo(before));
            var next=Assert.ThrowsAsync<LivLivError>(async()=>await host.Begin());Assert.That(next.code,Is.EqualTo("context-lost"));
            Assert.That(driver.released,Is.Zero);host.Dispose();host.Dispose();Assert.That(driver.released,Is.EqualTo(1));Assert.That(host.frees,Is.EqualTo(1));
            await Task.CompletedTask;
        }
        [Test]public void DisposedHostDoesNotNotifyAndThrowingCallbackCannotUndoLoss()
        {
            var host=new Host();host.OnContextLost=()=>throw new InvalidOperationException("callback");
            Assert.Throws<InvalidOperationException>(()=>host.NotifyContextLost());Assert.That(host.ContextLost,Is.True);
            Assert.DoesNotThrow(()=>host.NotifyContextLost());host.Dispose();
            var disposed=new Host();disposed.Dispose();disposed.OnContextLost=()=>Assert.Fail("Disposed host notified");disposed.NotifyContextLost();
        }
        [Test]public async Task LossCompletesPublicTaskWithoutWaitingForUnderlyingContinuation()
        {
            var host=new Host();var pending=new TaskCompletionSource<int>();var load=host.Held(pending.Task);
            host.NotifyContextLost();Assert.That(load.IsFaulted,Is.True);Assert.That(host.Poster,Is.Null);
            var error=Assert.ThrowsAsync<LivLivError>(async()=>await load);Assert.That(error.code,Is.EqualTo("context-lost"));
            pending.SetResult(1);await Task.Yield();Assert.That(load.IsFaulted,Is.True);host.Dispose();
        }
    }
}
