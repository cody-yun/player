using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
#if !LIVLIV_STANDALONE
using UnityEngine;
#endif

namespace LivLiv.Tests
{
    public sealed class ParityTests
    {
        private JObject data;
        internal static string RepoRoot
        {
            get
            {
#if LIVLIV_STANDALONE
                return Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../.."));
#else
                return Path.GetFullPath(Path.Combine(Application.dataPath, "../.."));
#endif
            }
        }
        [OneTimeSetUp]
        public void LoadReference()
        {
            data = JObject.Parse(File.ReadAllText(Path.Combine(RepoRoot,
                "unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/parity.json")));
        }
        private static void Near(double actual, JToken expected, string context, double tolerance = 1e-9)
            => Assert.That(actual, Is.EqualTo((double)expected).Within(tolerance), context);
        private static void ObjectNear(object actual, JToken expected, string context)
        {
            var obj = JObject.FromObject(actual);
            foreach (var p in ((JObject)expected).Properties()) Near((double)obj[p.Name], p.Value, context + "/" + p.Name);
        }
        [Test]
        public void FixtureIsBoundToExactSourceBytes()
        {
            var root = RepoRoot;
            if (!Directory.Exists(Path.Combine(root, "src/runtime"))) Assert.Ignore("External package consumer: upstream checkout not present.");
            foreach (var p in ((JObject)data["sourceHashes"]).Properties())
            {
                using (var sha = SHA256.Create())
                {
                    var hex = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root, p.Name)))).Replace("-", "").ToLowerInvariant();
                    Assert.That(hex, Is.EqualTo((string)p.Value), p.Name + " changed: review mapping before regenerating fixtures");
                }
            }
        }
        [Test]
        public void ParameterDefaultsRangesAndSmoothMatch()
        {
            ObjectNear(new Params(), data["defaults"], "default");
            CollectionAssert.AreEquivalent(((JObject)data["ranges"]).Properties().Select(p => p.Name), Params.Ranges.Keys);
            foreach (var p in ((JObject)data["ranges"]).Properties()) CollectionAssert.AreEqual(p.Value.ToObject<double[]>(), Params.Ranges[p.Name]);
            foreach (var r in data["smooth"]) Near(Params.Smooth((double)r["a"], (double)r["b"], (double)r["x"]), r["expected"], r.ToString());
        }
        [Test]
        public void OneEuroSequenceIncludesResetAndNonpositiveDelta()
        {
            var f = new OneEuro(.5, .05);
            foreach (var r in data["filters"])
            {
                if ((bool)r["reset"]) f.Reset();
                Near(f.Filter((double)r["v"], (double)r["dt"]), r["expected"], "filter");
            }
        }
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        public void ActionsMatchIncludingRetriggerAndAway(int index)
        {
            var test = data["actions"][index]; var a = new ActionPlayer(); a.Play((string)test["id"]);
            int i = 0;
            foreach (var r in test["rows"])
            {
                if (r["play"].Type != JTokenType.Null) a.Play((string)r["play"]);
                if (r["setAway"].Type != JTokenType.Null) a.SetAway((bool)r["setAway"], true);
                var offsets = a.Update((double)r["dt"]);
                CollectionAssert.AreEquivalent(((JObject)r["offsets"]).Properties().Select(p => p.Name), offsets.Keys);
                foreach (var p in ((JObject)r["offsets"]).Properties()) Near(offsets[p.Name], p.Value, "action/" + p.Name);
                Near(a.y, r["y"], "y"); Near(a.awayW, r["awayW"], "awayW");
                Assert.That(a.Busy, Is.EqualTo((bool)r["busy"]));
                Assert.That(a.away, Is.EqualTo((bool)r["away"])); Assert.That(a.auto, Is.EqualTo((bool)r["auto"]));
                ObjectNear(a.AwayPose(i++ / 60d), r["pose"], "awayPose");
            }
        }
        [Test]
        public void VowelCueRetainsLearnedCenterWhenReset()
        {
            var cue = new VowelCue();
            foreach (var r in data["vowels"])
            {
                if ((bool)r["reset"]) cue.Reset();
                Near(cue.Push(r["power"].ToObject<float[]>(), (double)r["hz"], (double)r["level"], (double)r["dt"]), r["expected"], "vowel");
                Near(cue.center, r["center"], "center");
            }
        }
        [TestCase(0), TestCase(1), TestCase(2), TestCase(3)]
        public void FftVowelTrackMatchesAtDifferentSampleRates(int index)
        {
            var r = data["tracks"][index];
            var x = r["x"].ToObject<float[]>(); var env = r["env"].ToObject<float[]>();
            var result = Vowel.Track(x, (double)r["sr"], env, (double)r["fps"], (double)r["lead"]);
            for (int i = 0; i < result.Length; i++) Near(result[i], r["expected"][i], "track/" + i, 2e-6);
            // Chunked evaluation must retain voice state and give the same clip result.
            var cue = new VowelCue(); var output = new float[env.Length];
            Vowel.Track(x, (double)r["sr"], env, (double)r["fps"], (double)r["lead"], cue, 0, 4, output);
            Vowel.Track(x, (double)r["sr"], env, (double)r["fps"], (double)r["lead"], cue, 4, null, output);
            CollectionAssert.AreEqual(result, output);
        }
        [Test]
        public void VoiceNoiseFloorGainAttackReleaseMatch()
        {
            var meter = new VoiceMeter(); var samples = new float[1024];
            foreach (var r in data["voice"])
            {
                for (int i = 0; i < samples.Length; i++) samples[i] = (float)r["sample"];
                meter.gain = (double)r["gain"];
                Near(meter.Read(samples, null, 0, (double)r["dt"]), r["expected"], "voice");
            }
        }
        [TestCase(0), TestCase(1), TestCase(2)]
        public void TrackingMatchesFull24SecondUpstreamTrace(int index)
        {
            var test = data["tracking"][index]; var c = test["config"]; var tracker = new FaceTracker(); var pose = new Params();
            tracker.SetMirror((bool)c["mirror"]); tracker.SetSmoothing((double)c["smoothing"]); tracker.blinkSync = (string)c["sync"];
            foreach (var r in test["rows"])
            {
                if ((bool)r["toggle"]) tracker.SetMirror(!tracker.mirror);
                if ((bool)r["calibrate"]) tracker.Calibrate();
                var ok = tracker.Poll(r["frame"].ToObject<FaceFrame>(), (double)r["now"]);
                Assert.That(ok, Is.EqualTo((bool)r["ok"]), "face present at " + r["now"]);
                if (ok) tracker.Apply(pose, (double)r["dt"]);
                ObjectNear(tracker.target, r["target"], "target at " + r["now"]);
                ObjectNear(pose, r["params"], "params at " + r["now"]);
                ObjectNear(tracker.pos, r["pos"], "pos"); ObjectNear(tracker.zero, r["zero"], "zero");
                Assert.That(tracker.calibrated, Is.EqualTo((bool)r["calibrated"]));
                Near(tracker.lostFor, r["lostFor"], "lostFor");
            }
        }
        [Test]
        public void TraceClockLoopsAndNeverRepeatsAFrame()
        {
            CollectionAssert.AreEqual(Trace.BS, data["BS"].ToObject<string[]>());
            CollectionAssert.AreEqual(Trace.LM, data["LM"].ToObject<int[]>());
            var src = new TraceSource(new[] { new FaceFrame { t = 0 }, new FaceFrame { t = 33, b = new[] { .1 } }, new FaceFrame { t = 66, b = new[] { .2 } } });
            foreach (var r in data["trace"])
            {
                Near(src.At((double)r["now"]), r["at"], "trace clock");
                var actual = src.Next((double)r["now"]);
                if (r["expected"].Type == JTokenType.Null) Assert.That(actual, Is.Null);
                else { Assert.That(actual, Is.Not.Null); Near(actual.t, r["expected"]["t"], "trace frame"); }
            }
        }
    }
}
