using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace LivLiv.Tests
{
    public sealed class SpeechWavTests
    {
        private static readonly Lazy<JObject> Data = new Lazy<JObject>(() =>
        {
            using (var f = File.OpenRead(Path.Combine(ParityTests.RepoRoot, "unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech-wav.json.gz")))
            using (var z = new GZipStream(f, CompressionMode.Decompress))
            using (var t = new StreamReader(z)) using (var j = new JsonTextReader(t)) return JObject.Load(j);
        });
        public static IEnumerable<TestCaseData> Cases() => Data.Value["cases"].Select(r => new TestCaseData(r).SetName("Speech WAV " + r["name"]));
        [TestCaseSource(nameof(Cases))]
        public void SplitHeaderAndFirstChannelPcmMatchOriginalIncoming(JToken row)
        {
            var bytes = Convert.FromBase64String((string)row["bytes"]); var sizes = row["sizes"].ToObject<int[]>();
            var actual = new List<float>();
            using (var wav = new SpeechWavReader())
            {
                for (int at = 0, i = 0; at < bytes.Length; i++)
                { int n = Math.Min(sizes[i % sizes.Length], bytes.Length - at); actual.AddRange(wav.Push(bytes, at, n)); at += n; }
                actual.AddRange(wav.Push(Array.Empty<byte>(), 0, 0, true));
                CollectionAssert.AreEqual(row["samples"].ToObject<float[]>(), actual);
                bool fallback = row["fallback"].Type != JTokenType.Null;
                Assert.That(wav.NeedsDecoder, Is.EqualTo(fallback));
                if (fallback) CollectionAssert.AreEqual(Convert.FromBase64String((string)row["fallback"]), wav.DecoderBytes());
                else Assert.That(wav.SampleRate, Is.EqualTo((int)row["rate"]));
            }
        }
        [Test]
        public void MalformedZeroChannelsAndByteLimitAreRejected()
        {
            var row = Data.Value["cases"].First(r => (string)r["name"] == "one delivery");
            var bytes = Convert.FromBase64String((string)row["bytes"]); bytes[22] = bytes[23] = 0;
            using (var wav = new SpeechWavReader()) Assert.Throws<InvalidDataException>(() => wav.Push(bytes, 0, bytes.Length));
            using (var wav = new SpeechWavReader(10)) Assert.Throws<InvalidDataException>(() => wav.Push(bytes, 0, 11));
        }
        [Test]
        public async Task IncomingJoinsBufferedChunksThenEnds()
        {
            using (var input = new SpeechIncomingBuffer())
            {
                input.Push(new[] { .1f, .2f }, 16000); input.Push(new[] { .3f }, 16000); input.Finish();
                CollectionAssert.AreEqual(new[] { .1f, .2f, .3f }, await input.NextAsync());
                Assert.That(input.SampleRate, Is.EqualTo(16000)); Assert.That(await input.NextAsync(), Is.Null);
            }
        }
        [Test]
        public async Task CancellationWakesPendingReaderAndDropsLateChunks()
        {
            using (var cancel = new CancellationTokenSource()) using (var input = new SpeechIncomingBuffer(cancel.Token))
            {
                var pending = input.NextAsync(); Assert.That(pending.IsCompleted, Is.False);
                cancel.Cancel(); input.Push(new[] { .2f }, 24000); input.Finish();
                await Cancelled(pending); await Cancelled(input.NextAsync());
            }
        }
        [Test]
        public async Task PartialFailureYieldsArrivedSamplesBeforeErrorAndCapsTotalSamples()
        {
            using (var input = new SpeechIncomingBuffer(maxSamples: 3))
            {
                var pending = input.NextAsync(); input.Push(new[] { .1f, .2f }, 24000);
                CollectionAssert.AreEqual(new[] { .1f, .2f }, await pending);
                Assert.Throws<InvalidDataException>(() => input.Push(new[] { .3f, .4f }, 24000));
                input.Push(new[] { .3f }, 24000); input.Finish(new IOException("network"));
                CollectionAssert.AreEqual(new[] { .3f }, await input.NextAsync());
                try { await input.NextAsync(); Assert.Fail("failure missing"); } catch (IOException) { }
            }
        }
        [Test]
        public void FixtureUsesCurrentSource()
        {
            using (var sha = SHA256.Create()) foreach (var e in ((JObject)Data.Value["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot, e.Name)))).Replace("-", "").ToLowerInvariant(), Is.EqualTo((string)e.Value));
        }
        private static async Task Cancelled(Task<float[]> task)
        { try { await task; Assert.Fail("cancellation missing"); } catch (OperationCanceledException) { } }
    }
}
