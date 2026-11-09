using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LivLiv.Tests
{
    public sealed class SpeechStateTests
    {
        private static JObject Fixture => JObject.Parse(File.ReadAllText(Path.Combine(ParityTests.RepoRoot,
            "unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech-state.json")));
        private sealed class Loader : ISpeechLoader
        { public ISpeechIncoming Open(string url, CancellationToken token) => throw new NotSupportedException(); }
        private class Output : ISpeechOutput
        {
            public bool running, disposed;
            public bool Running => !disposed && running;
            public double CurrentTime => 0;
            public double HeardTime => 0;
            public double WallTime => 0;
            public virtual void Unlock() { }
            public void SetVolume(double value) { }
            public ISpeechPiece Schedule(float[] samples, int rate, double at) => throw new NotSupportedException();
            public void Dispose() { disposed = true; }
        }
        private sealed class EventOutput : Output, ISpeechStateOutput
        {
            public string initial = "suspended", state;
            public int events;
            public bool throwOnCloseQuery;
            public override void Unlock() { if (state == null) { state = initial; running = state == "running"; } }
            public void Change(string value) { if (state == value) return; state = value; running = state == "running"; events++; }
            public int ConsumeStateChanges() { int count = events; events = 0; return count; }
            public bool HasOpenContext => throwOnCloseQuery ? throw new InvalidOperationException("state query") : !disposed && state != null && state != "closed";
        }
        public static IEnumerable<TestCaseData> Cases() => Fixture["scenarios"].Select(s => new TestCaseData(s).SetName("Speech state " + s["name"]));
        [TestCaseSource(nameof(Cases))]
        public void StateObservationsMatchOriginalContextEvents(JToken scenario)
        {
            var output = new EventOutput { initial = (string)scenario["initial"] };
            var speech = new SpeechPlayer(output, new Loader()); var events = new List<bool>();
            speech.onState = () => events.Add(speech.Running);
            try
            {
                foreach (var row in scenario["states"])
                {
                    string operation = (string)row["operation"];
                    if (operation == "unlock") speech.Unlock();
                    else if (operation == "stop") speech.Stop();
                    else if (operation == "dispose") speech.Dispose();
                    else if (operation != "created") output.Change(operation);
                    speech.Tick();
                    Assert.That(speech.Running, Is.EqualTo((bool)row["running"]), operation);
                    Assert.That(speech.Busy, Is.EqualTo((bool)row["busy"]), operation);
                    CollectionAssert.AreEqual(row["events"].ToObject<bool[]>(), events, operation);
                }
            }
            finally { speech.Dispose(); }
        }
        [Test]
        public void ExistingOutputsNeedNoNewInterfaceAndDetectRunningChangesOnce()
        {
            var output = new Output { running = true }; var speech = new SpeechPlayer(output, new Loader());
            var events = new List<bool>(); speech.onState = () => events.Add(speech.Running);
            speech.Tick(); speech.Unlock(); speech.Stop(); speech.Tick(); Assert.That(events, Is.Empty);
            output.running = false; speech.Tick(); speech.Tick(); output.running = true; speech.Tick();
            CollectionAssert.AreEqual(new[] { false, true }, events);
            speech.Dispose(); speech.Tick(); speech.Dispose(); CollectionAssert.AreEqual(new[] { false, true, false }, events);
        }
        [Test]
        public void MultipleContextEventsAreNotLostWhenRunningReturnsToTheSameValue()
        {
            var output = new EventOutput { initial = "running" }; var speech = new SpeechPlayer(output, new Loader());
            int calls = 0; speech.onState = () => calls++; speech.Unlock();
            output.Change("suspended"); output.Change("running"); speech.Tick(); speech.Tick();
            Assert.That(calls, Is.EqualTo(2)); speech.onState = null; speech.Dispose();
        }
        [Test]
        public void StateCallbackCanReenterTickStopAndDisposeWithoutRepeatingEvents()
        {
            var output = new EventOutput(); var speech = new SpeechPlayer(output, new Loader());
            speech.Unlock(); int calls = 0;
            speech.onState = () => { calls++; speech.Tick(); speech.Stop(); speech.Dispose(); };
            output.Change("running"); output.Change("suspended"); speech.Tick();
            Assert.That(calls, Is.EqualTo(2)); // observed state then one final closed notification
            Assert.That(output.disposed, Is.True); Assert.That(speech.onState, Is.Null);
        }
        [Test]
        public void CallbackAndCloseQueryFaultsDoNotPreventOutputCleanup()
        {
            var output = new EventOutput { throwOnCloseQuery = true }; var speech = new SpeechPlayer(output, new Loader());
            int errors = 0; speech.onError = e => errors++; speech.onState = () => throw new Exception("listener");
            speech.Unlock(); output.Change("running"); speech.Tick(); Assert.That(errors, Is.EqualTo(1));
            speech.Dispose(); Assert.That(output.disposed, Is.True); Assert.That(errors, Is.EqualTo(3));
        }
        [Test]
        public void FixtureUsesUnmodifiedSource()
        {
            using (var sha = SHA256.Create()) foreach (var p in ((JObject)Fixture["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot, p.Name)))).Replace("-", "").ToLowerInvariant(), Is.EqualTo((string)p.Value));
        }
    }
}
