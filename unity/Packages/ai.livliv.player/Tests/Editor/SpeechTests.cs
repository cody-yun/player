using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LivLiv.Tests
{
    public sealed class SpeechTests
    {
        private sealed class Surface : PlayerBase
        {
            private readonly Dictionary<string, double> expressions = new Dictionary<string, double>();
            public PlayerFrame Frame => frame;
            public Surface() : base(new Params(), new PlayerOptions { idle = false }) { }
            protected override bool Drawable => true;
            protected override double Clock => 0;
            protected override IDictionary<string, double> ShownExpressions => expressions;
            protected override IEnumerable<string> ExpressionIds => Array.Empty<string>();
            protected override Puppet Seen => null;
            protected override double DisplayWidth => 1;
            protected override double DisplayHeight => 1;
            protected override void Show(double dt) { }
            protected override void Free() { }
        }
        private sealed class Output : ISpeechOutput
        {
            internal sealed class Piece : ISpeechPiece
            {
                public Output owner;
                public double at;
                public int n, rate, disposed;
                public bool Ended => owner.time >= at + (double)n / rate;
                public void Dispose() { disposed++; }
            }
            public readonly List<Piece> pieces = new List<Piece>();
            public double time, wall, lag, volume = 1;
            public int disposed, unlocks;
            public bool running = true;
            public bool Running => running;
            public double CurrentTime => time;
            public double HeardTime => time - lag;
            public double WallTime => wall;
            public void Unlock() { unlocks++; }
            public void SetVolume(double value) { volume = value; }
            public ISpeechPiece Schedule(float[] samples, int rate, double at)
            {
                var p = new Piece { owner = this, n = samples.Length, rate = rate, at = at };
                pieces.Add(p); return p;
            }
            public void Dispose() { disposed++; }
            public void Advance(double seconds) { time = seconds; wall = seconds; }
        }
        private sealed class Incoming : ISpeechIncoming
        {
            private readonly Queue<float[]> buffered = new Queue<float[]>();
            private TaskCompletionSource<float[]> waiting;
            private bool complete;
            public int disposed, rate = 24000;
            public bool cancelled;
            public int SampleRate => rate;
            public Task<float[]> NextAsync()
            {
                if (buffered.Count > 0) return Task.FromResult(buffered.Dequeue());
                if (complete) return Task.FromResult<float[]>(null);
                waiting = new TaskCompletionSource<float[]>(); return waiting.Task;
            }
            public void Push(float[] x)
            {
                var waiter = waiting; waiting = null;
                if (waiter != null) waiter.SetResult(x); else buffered.Enqueue(x);
            }
            public void Finish()
            {
                complete = true; var waiter = waiting; waiting = null; waiter?.SetResult(null);
            }
            public void Fail()
            {
                complete = true; var waiter = waiting; waiting = null; waiter?.SetException(new IOException("download interrupted"));
            }
            public void Dispose() { disposed++; }
        }
        private sealed class Loader : ISpeechLoader
        {
            public bool firstAlreadyComplete;
            public readonly List<Incoming> inputs = new List<Incoming>();
            public readonly List<CancellationToken> tokens = new List<CancellationToken>();
            public ISpeechIncoming Open(string url, CancellationToken token)
            {
                var input = new Incoming(); inputs.Add(input); tokens.Add(token);
                if (firstAlreadyComplete && inputs.Count == 1) input.Finish();
                token.Register(() => input.cancelled = true); return input;
            }
        }
        private static readonly Lazy<JObject> Data = new Lazy<JObject>(() =>
        {
            using (var file = File.OpenRead(Path.Combine(ParityTests.RepoRoot, "unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech.json.gz")))
            using (var zip = new GZipStream(file, CompressionMode.Decompress))
            using (var text = new StreamReader(zip))
            using (var json = new JsonTextReader(text)) return JObject.Load(json);
        });
        public static IEnumerable<TestCaseData> Envelopes() => Data.Value["envelopes"].Select(r => new TestCaseData(r).SetName("Speech envelope " + r["rate"] + " " + r["kind"]));
        public static IEnumerable<TestCaseData> Streams() => Data.Value["streams"].Select(r => new TestCaseData(r).SetName("Speech stream " + r["rate"]));

        [TestCaseSource(nameof(Envelopes))]
        public void EnvelopeMatchesUpstream(JToken row)
        {
            var actual = SpeechPlayer.Envelope(row["samples"].ToObject<float[]>(), (int)row["rate"]);
            var expected = row["expected"].ToObject<float[]>();
            Assert.That(actual.Length, Is.EqualTo(expected.Length));
            for (int i = 0; i < actual.Length; i++) Assert.That(actual[i], Is.EqualTo(expected[i]).Within(2e-7), "frame " + i);
        }

        [TestCaseSource(nameof(Streams))]
        public void StreamScheduleAndInterpolatedTracksMatchUpstream(JToken row)
        {
            var output = new Output(); var loader = new Loader();
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.Enqueue("sentence"); var input = loader.inputs[0]; input.rate = (int)row["rate"];
                foreach (var op in row["operations"])
                {
                    output.Advance((double)op["time"]);
                    input.Push(op["samples"].ToObject<float[]>());
                    if ((bool)op["complete"]) input.Finish();
                    foreach (var read in op["reads"])
                    {
                        output.Advance((double)read["time"]);
                        Assert.That(speech.Read(), Is.EqualTo((double)read["mouth"]).Within(2e-7), "mouth at " + read["time"]);
                        Assert.That(speech.Vowel(), Is.EqualTo((double)read["vowel"]).Within(2e-6), "vowel at " + read["time"]);
                    }
                }
                Assert.That(output.pieces.Count, Is.EqualTo(row["scheduled"].Count()));
                for (int i = 0; i < output.pieces.Count; i++)
                {
                    Assert.That(output.pieces[i].at, Is.EqualTo((double)row["scheduled"][i]["at"]).Within(1e-12));
                    Assert.That(output.pieces[i].n, Is.EqualTo((int)row["scheduled"][i]["n"]));
                    Assert.That(output.pieces[i].rate, Is.EqualTo((int)row["scheduled"][i]["rate"]));
                }
            }
        }

        [Test]
        public void DownloadsStartTogetherButPlaybackRemainsInEnqueueOrder()
        {
            var output = new Output(); var loader = new Loader(); var calls = new List<string>(); int idle = 0;
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.onIdle = () => idle++;
                speech.Enqueue("one", Hooks("one", calls)); speech.Enqueue("two", Hooks("two", calls));
                Assert.That(loader.inputs.Count, Is.EqualTo(2)); Assert.That(speech.Busy, Is.True);
                loader.inputs[1].Push(new float[4800]); loader.inputs[1].Finish();
                Assert.That(output.pieces, Is.Empty);
                loader.inputs[0].Push(new float[2400]); loader.inputs[0].Finish();
                Assert.That(output.pieces.Count, Is.EqualTo(2));
                Assert.That(output.pieces[0].at, Is.EqualTo(.04)); Assert.That(output.pieces[1].at, Is.EqualTo(.14).Within(1e-12));
                output.Advance(.06); speech.Tick();
                CollectionAssert.AreEqual(new[] { "one:start" }, calls);
                output.Advance(.15); speech.Tick();
                CollectionAssert.AreEqual(new[] { "one:start", "one:end", "two:start" }, calls);
                output.Advance(.4); speech.Tick(); speech.Tick();
                CollectionAssert.AreEqual(new[] { "one:start", "one:end", "two:start", "two:end" }, calls);
                Assert.That(speech.Busy, Is.False); Assert.That(idle, Is.EqualTo(1));
                Assert.That(output.pieces.All(p => p.disposed == 1), Is.True);
            }
        }

        [Test]
        public void SynchronousIdleCallbackCanEnqueueWithoutLosingQueueOrder()
        {
            var output = new Output(); var loader = new Loader { firstAlreadyComplete = true };
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.onIdle = () => { if (loader.inputs.Count == 1) speech.Enqueue("middle"); };
                speech.Enqueue("empty"); speech.Enqueue("last");
                Assert.That(loader.inputs.Count, Is.EqualTo(3));
                loader.inputs[2].Push(new float[4800]); loader.inputs[2].Finish();
                Assert.That(output.pieces, Is.Empty);
                loader.inputs[1].Push(new float[2400]); loader.inputs[1].Finish();
                Assert.That(output.pieces.Select(p => p.n), Is.EqualTo(new[] { 2400, 4800 }));
                Assert.That(output.pieces[1].at, Is.EqualTo(.14).Within(1e-12));
            }
        }

        [Test]
        public void OutputLatencyDelaysMouthHooksAndRetirement()
        {
            var output = new Output { lag = .1 }; var loader = new Loader(); var calls = new List<string>();
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.Enqueue("one", Hooks("one", calls));
                loader.inputs[0].Push(Data.Value["streams"][1]["operations"][0]["samples"].ToObject<float[]>()); loader.inputs[0].Finish();
                output.Advance(.08); speech.Tick(); Assert.That(calls, Is.Empty); Assert.That(speech.Read(), Is.Zero);
                output.Advance(.17); speech.Tick(); Assert.That(calls, Does.Contain("one:start")); Assert.That(speech.Read(), Is.GreaterThan(0));
                output.Advance(.23); speech.Tick(); Assert.That(speech.Busy, Is.True); Assert.That(calls, Does.Not.Contain("one:end"));
                output.Advance(.34); speech.Tick(); Assert.That(speech.Busy, Is.False);
                CollectionAssert.AreEqual(new[] { "one:start", "one:end" }, calls);
            }
        }

        [Test]
        public void StopFailsEachQueuedCopyOnceAndDoesNotWaitForAnOldDownload()
        {
            var output = new Output(); var loader = new Loader(); int failures = 0, idle = 0;
            using (var speech = new SpeechPlayer(output, loader))
            {
                var hooks = new ClipHooks { onFail = () => failures++ };
                speech.onIdle = () => idle++;
                speech.Enqueue("old", hooks); speech.Enqueue("also-old", hooks);
                speech.Stop(); speech.Stop();
                Assert.That(failures, Is.EqualTo(2)); Assert.That(idle, Is.Zero); Assert.That(speech.Busy, Is.False);
                Assert.That(loader.tokens.All(t => t.IsCancellationRequested), Is.True);
                speech.Enqueue("new"); loader.inputs[2].Push(new float[2400]); loader.inputs[2].Finish();
                Assert.That(output.pieces.Count, Is.EqualTo(1));
                loader.inputs[0].Push(new float[4800]); loader.inputs[1].Finish();
                Assert.That(output.pieces.Count, Is.EqualTo(1)); Assert.That(failures, Is.EqualTo(2));
                output.Advance(.2); speech.Tick(); Assert.That(idle, Is.EqualTo(1));
            }
        }

        [Test]
        public void EmptyOrFailedDownloadsDoNotBlockFollowingSpeech()
        {
            var output = new Output(); var loader = new Loader(); var calls = new List<string>(); int errors = 0;
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.onError = _ => errors++;
                speech.Enqueue("empty", Hooks("empty", calls)); speech.Enqueue("failed", Hooks("failed", calls)); speech.Enqueue("valid", Hooks("valid", calls));
                loader.inputs[0].Finish(); loader.inputs[1].Fail(); loader.inputs[2].Push(new float[2400]); loader.inputs[2].Finish();
                output.Advance(.2); speech.Tick();
                CollectionAssert.AreEqual(new[] { "empty:fail", "failed:fail", "valid:start", "valid:end" }, calls);
                Assert.That(errors, Is.EqualTo(1)); Assert.That(speech.Busy, Is.False);
            }
        }

        [Test]
        public void InterruptedStreamKeepsItsArrivedSoundThenEnds()
        {
            var output = new Output(); var loader = new Loader(); var calls = new List<string>();
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.Enqueue("partial", Hooks("partial", calls)); loader.inputs[0].Push(new float[2400]); loader.inputs[0].Fail();
                output.Advance(.2); speech.Tick();
                CollectionAssert.AreEqual(new[] { "partial:start", "partial:end" }, calls);
                Assert.That(loader.inputs[0].disposed, Is.EqualTo(1));
            }
        }

        [Test]
        public void SuspendedClockWatchdogFailsAndReturnsIdle()
        {
            var output = new Output { running = false }; var loader = new Loader(); var calls = new List<string>(); int idle = 0;
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.onIdle = () => idle++;
                speech.Enqueue("blocked", Hooks("blocked", calls)); loader.inputs[0].Push(new float[2400]); loader.inputs[0].Finish();
                output.wall = 3; speech.Tick();
                CollectionAssert.AreEqual(new[] { "blocked:fail" }, calls);
                Assert.That(speech.Busy, Is.False); Assert.That(idle, Is.EqualTo(1));
                Assert.That(output.pieces[0].disposed, Is.EqualTo(1));
            }
        }

        [Test]
        public void StartCallbackCanStopWithoutReceivingEnd()
        {
            var output = new Output(); var loader = new Loader(); var calls = new List<string>();
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.Enqueue("one", new ClipHooks { onStart = () => { calls.Add("start"); speech.Stop(); }, onEnd = () => calls.Add("end"), onFail = () => calls.Add("fail") });
                loader.inputs[0].Push(new float[2400]); loader.inputs[0].Finish();
                output.Advance(1); speech.Tick();
                CollectionAssert.AreEqual(new[] { "start", "fail" }, calls); Assert.That(speech.Busy, Is.False);
            }
        }

        [Test]
        public void FaultingHookDoesNotStrandOtherHooksOrResources()
        {
            var output = new Output(); var loader = new Loader(); int failures = 0, errors = 0;
            using (var speech = new SpeechPlayer(output, loader))
            {
                speech.onError = _ => errors++;
                speech.Enqueue("one", new ClipHooks { onFail = () => throw new Exception("hook") });
                speech.Enqueue("two", new ClipHooks { onFail = () => failures++ });
                speech.Stop(); Assert.That(failures, Is.EqualTo(1)); Assert.That(errors, Is.EqualTo(1));
            }
        }

        [Test]
        public void DisposeReleasesOutputOnceAndRejectsReentrantEnqueue()
        {
            var output = new Output(); var loader = new Loader(); int failures = 0; bool rejected = false;
            var speech = new SpeechPlayer(output, loader);
            speech.Enqueue("one", new ClipHooks { onFail = () =>
            {
                failures++;
                try { speech.Enqueue("late"); } catch (ObjectDisposedException) { rejected = true; }
            } });
            loader.inputs[0].Push(new float[2400]);
            speech.SetVolume(.25); Assert.That(output.volume, Is.EqualTo(.25));
            speech.Dispose(); speech.Dispose(); loader.inputs[0].Finish();
            Assert.That(failures, Is.EqualTo(1)); Assert.That(output.disposed, Is.EqualTo(1));
            Assert.That(rejected, Is.True);
            Assert.That(output.pieces[0].disposed, Is.EqualTo(1)); Assert.That(speech.Busy, Is.False);
            Assert.That(speech.Read(), Is.Zero); Assert.That(speech.Vowel(), Is.Zero);
        }

        [Test]
        public void PublicSpeechDrivesMouthStopCanReplayAndPlayerOwnsItsRelease()
        {
            var output = new Output(); var loader = new Loader();
            var runtime = new SpeechPlayer(output, loader);
            using (var player = new Surface())
            using (var speech = Speech.Attach(player, runtime, new LipSyncOptions { motion = 0 }))
            {
                var samples = Data.Value["streams"][1]["operations"][0]["samples"].ToObject<float[]>();
                speech.Say("one"); loader.inputs[0].Push(samples); loader.inputs[0].Finish();
                output.Advance(.1); player.Tick(1d / 60);
                Assert.That(player.Frame.@params.mouthOpen, Is.EqualTo(runtime.Read()).Within(1e-12));
                Assert.That(player.Frame.@params.mouthOpen, Is.GreaterThan(0));
                Assert.That(player.Frame.@params.vowel, Is.EqualTo(runtime.Vowel()).Within(1e-12));
                speech.Stop(); player.Tick(1d / 60);
                Assert.That(speech.Busy, Is.False); Assert.That(player.Frame.@params.mouthOpen, Is.Zero);
                speech.Say("two"); loader.inputs[1].Push(samples); loader.inputs[1].Finish();
                output.Advance(.2); player.Tick(1d / 60);
                Assert.That(player.Frame.@params.mouthOpen, Is.GreaterThan(0));
                player.Dispose(); Assert.That(speech.Busy, Is.False); Assert.That(output.disposed, Is.EqualTo(1));
                Assert.Throws<ObjectDisposedException>(() => speech.Say("late"));
            }
            Assert.That(output.disposed, Is.EqualTo(1));
        }

        [Test]
        public void FixtureUsesCurrentSources()
        {
            using (var sha = SHA256.Create()) foreach (var entry in ((JObject)Data.Value["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot, entry.Name)))).Replace("-", "").ToLowerInvariant(), Is.EqualTo((string)entry.Value), entry.Name);
        }
        private static ClipHooks Hooks(string name, List<string> calls) => new ClipHooks { onStart = () => calls.Add(name + ":start"), onEnd = () => calls.Add(name + ":end"), onFail = () => calls.Add(name + ":fail") };
    }
}
