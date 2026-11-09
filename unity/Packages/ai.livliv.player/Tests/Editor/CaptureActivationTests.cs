using System;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class CaptureActivationTests
    {
        private static async Task Throws<T>(Task task) where T:Exception
        {
            try{await task;Assert.Fail("Expected "+typeof(T).Name);}
            catch(T){}
        }
        [Test] public async Task GrantDuringDialogPauseWaitsForForeground()
        {
            var gate=new CaptureActivationGate();var permission=new TaskCompletionSource<bool>();
            var start=gate.AuthorizeAsync(_=>permission.Task,default);
            Assert.That(gate.SetPaused(true),Is.False);
            permission.SetResult(true);
            Assert.That(start.IsCompleted,Is.False,"No capture while backgrounded");
            Assert.That(gate.IsAuthorizing,Is.True);
            gate.SetPaused(false);await start;
            Assert.That(gate.IsAuthorizing,Is.False);
            Assert.That(gate.SetPaused(true),Is.True,"Ordinary background cancels device startup");
        }
        [Test] public async Task ForegroundBeforeGrantAlsoCompletes()
        {
            var gate=new CaptureActivationGate();var permission=new TaskCompletionSource<bool>();
            var start=gate.AuthorizeAsync(_=>permission.Task,default);
            gate.SetPaused(true);gate.SetPaused(false);
            Assert.That(start.IsCompleted,Is.False);
            permission.SetResult(true);await start;
        }
        [Test] public async Task DenialDoesNotLeaveAuthorizationOpen()
        {
            var gate=new CaptureActivationGate();
            await Throws<UnauthorizedAccessException>(gate.AuthorizeAsync(_=>Task.FromException(new UnauthorizedAccessException()),default));
            Assert.That(gate.IsAuthorizing,Is.False);Assert.That(gate.SetPaused(true),Is.True);
        }
        [Test] public async Task CancelWhileAwaitingForegroundThenRetry()
        {
            var gate=new CaptureActivationGate();gate.SetPaused(true);
            using(var cancel=new CancellationTokenSource())
            {
                var start=gate.AuthorizeAsync(_=>Task.CompletedTask,cancel.Token);
                cancel.Cancel();await Throws<TaskCanceledException>(start);
                Assert.That(gate.IsAuthorizing,Is.False);
            }
            var retry=gate.AuthorizeAsync(_=>Task.CompletedTask,default);
            Assert.That(retry.IsCompleted,Is.False);gate.SetPaused(false);await retry;
        }
        [Test] public async Task PrecancelDoesNotRequestPermission()
        {
            var gate=new CaptureActivationGate();bool requested=false;
            await Throws<OperationCanceledException>(gate.AuthorizeAsync(_=>{requested=true;return Task.CompletedTask;},new CancellationToken(true)));
            Assert.That(requested,Is.False);Assert.That(gate.IsAuthorizing,Is.False);
        }
        [Test] public async Task CancellationWhilePromptPendingPreventsLateGrantActivation()
        {
            var gate=new CaptureActivationGate();var permission=new TaskCompletionSource<bool>();
            using(var cancel=new CancellationTokenSource())
            {
                var start=gate.AuthorizeAsync(_=>permission.Task,cancel.Token);
                gate.SetPaused(true);cancel.Cancel();permission.SetResult(true);
                await Throws<OperationCanceledException>(start);
                gate.SetPaused(false);await gate.AuthorizeAsync(_=>Task.CompletedTask,default);
            }
        }
        [Test] public async Task SeparateDevicesDoNotPreserveUnrelatedStartup()
        {
            var camera=new CaptureActivationGate();var mic=new CaptureActivationGate();
            var permission=new TaskCompletionSource<bool>();var start=camera.AuthorizeAsync(_=>permission.Task,default);
            Assert.That(camera.SetPaused(true),Is.False);Assert.That(mic.SetPaused(true),Is.True);
            permission.SetResult(true);camera.SetPaused(false);await start;
        }
    }
}
