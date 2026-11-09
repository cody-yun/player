using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LivLiv.Tests
{
    public sealed class SpeechOutputTests
    {
        private GameObject listener;
        private bool oldPause;
        private float oldTimeScale;
        private sealed class Host : IPlayerHost, IDisposable
        {
            public readonly List<IPlayerDriver> drivers = new List<IPlayerDriver>();
            public Action Use(IPlayerDriver driver)
            { drivers.Add(driver); return () => { if (drivers.Remove(driver)) driver.Dispose(); }; }
            public void Blink() { }
            public void Dispose() { foreach (var d in drivers.ToArray()) d.Dispose(); drivers.Clear(); }
        }
        private sealed class Loader : ISpeechLoader
        {
            private sealed class Incoming : ISpeechIncoming
            {
                private float[] samples = Signal(24000, .2);
                public int SampleRate => 24000;
                public Task<float[]> NextAsync() { var next = samples; samples = null; return Task.FromResult(next); }
                public void Dispose() { samples = null; }
            }
            public ISpeechIncoming Open(string url, CancellationToken token) { token.ThrowIfCancellationRequested(); return new Incoming(); }
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.That(Application.isPlaying, Is.True);
            oldPause = AudioListener.pause; oldTimeScale = Time.timeScale;
            AudioListener.pause = false; Time.timeScale = 1;
            if (!UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Any(l => l.enabled))
            { listener = new GameObject("Speech test listener"); listener.AddComponent<AudioListener>(); }
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            AudioListener.pause = oldPause; Time.timeScale = oldTimeScale;
            if (listener != null) UnityEngine.Object.Destroy(listener);
            listener = null;
            yield return null;
        }
        private static float[] Signal(int rate, double duration)
        {
            var x = new float[(int)(rate * duration)];
            for (int i = 0; i < x.Length; i++) x[i] = (float)(.2 * Math.Sin(2 * Math.PI * 330 * i / rate));
            return x;
        }
        private static AudioSource[] Sources() => Resources.FindObjectsOfTypeAll<AudioSource>().Where(s => s.name == "LivLiv Speech Piece").ToArray();
        private static AudioClip[] Clips() => Resources.FindObjectsOfTypeAll<AudioClip>().Where(c => c.name == "LivLiv Speech PCM").ToArray();

        [UnityTest]
        public IEnumerator ScheduledPcmActuallyPassesThroughUnityAudioAndReleases()
        {
            var output = new UnitySpeechOutput(new UnitySpeechOptions { volume = .02, outputLatencySeconds = () => .03 });
            var samples = Signal(24000, .8);
            try
            {
                double start = output.CurrentTime + .2;
                var piece = output.Schedule(samples, 24000, start);
                var source = Sources().Single();
                Assert.That(output.ActivePieceCount, Is.EqualTo(1));
                Assert.That(source.spatialBlend, Is.Zero); Assert.That(source.pitch, Is.EqualTo(1));
                var copied = new float[samples.Length]; Assert.That(source.clip.GetData(copied, 0), Is.True);
                CollectionAssert.AreEqual(samples, copied);
                Assert.That(piece.Ended, Is.False);
                var observed = new float[1024]; double peak = 0;
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                // First call enables the per-source history; subsequent calls inspect actual mix output.
                source.GetOutputData(observed, 0);
                while (output.CurrentTime < start + .6 && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    source.GetOutputData(observed, 0);
                    foreach (float sample in observed) peak = Math.Max(peak, Math.Abs(sample));
                    yield return null;
                }
                Assert.That(output.CurrentTime, Is.GreaterThanOrEqualTo(start + .6), "DSP clock must advance");
                Assert.That(peak, Is.GreaterThan(1e-5), "nonzero PCM observed after Unity playback, not just buffer creation");
                Assert.That(output.OutputLatencySeconds, Is.EqualTo(.03));
                output.SetVolume(.1); Assert.That(source.volume, Is.EqualTo(.1f));
                piece.Dispose(); piece.Dispose(); Assert.That(output.ActivePieceCount, Is.Zero);
                Assert.That(source == null || source.clip == null, Is.True, "stop detaches the buffer immediately");
            }
            finally { output.Dispose(); }
            yield return null;
            Assert.That(Sources(), Is.Empty); Assert.That(Clips(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator FactoryDrainsQueueWhileRenderingAndTimeScaleAreStopped()
        {
            var host = new Host(); var speech = UnitySpeech.Create(host, new Loader(), new UnitySpeechOptions { volume = 0 });
            int idle = 0;
            try
            {
                speech.onIdle = () => idle++;
                Time.timeScale = 0;
                speech.Say("one"); speech.Say("two"); speech.Say("three");
                Assert.That(speech.Busy, Is.True); Assert.That(host.drivers.Count, Is.EqualTo(1));
                Assert.That(Sources().Length, Is.EqualTo(3));
                double deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (speech.Busy && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(speech.Busy, Is.False); Assert.That(idle, Is.EqualTo(1));
                // Stop retains the driver and supports a new reply.
                speech.Say("four"); speech.Stop();
                Assert.That(speech.Busy, Is.False); Assert.That(host.drivers.Count, Is.EqualTo(1));
                speech.Say("five"); Assert.That(speech.Busy, Is.True);
                host.Dispose(); Assert.That(speech.Busy, Is.False);
                Assert.Throws<ObjectDisposedException>(() => speech.Say("late"));
            }
            finally { speech.Dispose(); host.Dispose(); }
            yield return null;
            Assert.That(Sources(), Is.Empty); Assert.That(Clips(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator ListenerPauseFreezesScheduledAudioAndUnlockDoesNotChangeIt()
        {
            var output = new UnitySpeechOutput(new UnitySpeechOptions { volume = 0 });
            try
            {
                AudioListener.pause = true;
                double clock = output.CurrentTime;
                var piece = output.Schedule(Signal(24000, .2), 24000, clock + .1);
                output.Unlock();
                Assert.That(AudioListener.pause, Is.True); Assert.That(output.Running, Is.False);
                double until = Time.realtimeSinceStartupAsDouble + .15;
                while (Time.realtimeSinceStartupAsDouble < until) yield return null;
                Assert.That(output.CurrentTime, Is.EqualTo(clock).Within(.001)); Assert.That(piece.Ended, Is.False);
                AudioListener.pause = false;
                double deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (!piece.Ended && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(piece.Ended, Is.True);
            }
            finally { AudioListener.pause = false; output.Dispose(); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FactoryReportsListenerPauseResumeAndFinalCloseWhileTimeScaleIsZero()
        {
            var host = new Host(); var states = new List<bool>();
            var speech = UnitySpeech.Create(host, new Loader(), new UnitySpeechOptions { volume = 0, onState = running => states.Add(running) });
            try
            {
                Time.timeScale = 0; speech.Unlock(); yield return null;
                Assert.That(states, Is.Empty, "native engine was already running; unlock is not a state event");
                AudioListener.pause = true;
                double deadline = Time.realtimeSinceStartupAsDouble + 2;
                while (states.Count < 1 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                CollectionAssert.AreEqual(new[] { false }, states);
                speech.Stop(); speech.Unlock(); yield return null;
                Assert.That(AudioListener.pause, Is.True); CollectionAssert.AreEqual(new[] { false }, states);
                AudioListener.pause = false; deadline = Time.realtimeSinceStartupAsDouble + 2;
                while (states.Count < 2 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                CollectionAssert.AreEqual(new[] { false, true }, states);
                speech.Dispose(); CollectionAssert.AreEqual(new[] { false, true, false }, states);
                yield return null; speech.Dispose(); Assert.That(states.Count, Is.EqualTo(3));
            }
            finally { AudioListener.pause = false; speech.Dispose(); host.Dispose(); }
        }

        [UnityTest]
        public IEnumerator DefaultFactoryDecodesByteInputAndPlaysIt()
        {
            var pcm = Signal(24000, .4); byte[] encoded;
            using (var bytes = new MemoryStream()) using (var writer = new BinaryWriter(bytes))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + pcm.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(24000); writer.Write(48000); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length * 2);
                foreach (float x in pcm) writer.Write((short)Math.Round(x * 32767));
                encoded = bytes.ToArray();
            }
            var host = new Host(); Exception error = null;
            var speech = UnitySpeech.Create(host, options: new UnitySpeechOptions { volume = .02, onError = e => error = e });
            try
            {
                int idle = 0; speech.onIdle = () => idle++;
                speech.Say(encoded); Array.Clear(encoded, 0, encoded.Length);
                Assert.That(speech.Busy, Is.True);
                var values = new float[1024]; double peak = 0, deadline = Time.realtimeSinceStartupAsDouble + 5;
                AudioListener.GetOutputData(values, 0);
                while (speech.Busy && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    AudioListener.GetOutputData(values, 0);
                    foreach (float x in values) peak = Math.Max(peak, Math.Abs(x));
                    yield return null;
                }
                Assert.That(error, Is.Null); Assert.That(speech.Busy, Is.False);
                Assert.That(idle, Is.EqualTo(1)); Assert.That(peak, Is.GreaterThan(1e-5));
            }
            finally { speech.Dispose(); host.Dispose(); }
            yield return null;
            Assert.That(Sources(), Is.Empty); Assert.That(Clips(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator BackgroundAndDeviceResetNotificationsStopWithoutRemovingDriver()
        {
            var host = new Host(); var speech = UnitySpeech.Create(host, new Loader(), new UnitySpeechOptions { volume = 0 });
            try
            {
                var pump = Resources.FindObjectsOfTypeAll<UnitySpeechPump>().Single();
                speech.Say("background"); Assert.That(speech.Busy, Is.True);
                pump.SendMessage("OnApplicationPause", true);
                Assert.That(speech.Busy, Is.False); Assert.That(Sources().All(s => s.clip == null), Is.True);
                pump.SendMessage("OnApplicationPause", false);
                Assert.That(speech.Busy, Is.False, "foreground does not replay the interrupted reply");
                speech.Say("device-change"); Assert.That(speech.Busy, Is.True);
                pump.SendMessage("ConfigurationChanged", true);
                Assert.That(speech.Busy, Is.False); Assert.That(host.drivers.Count, Is.EqualTo(1));
                speech.Say("retry"); Assert.That(speech.Busy, Is.True);
            }
            finally { speech.Dispose(); host.Dispose(); }
            yield return null;
            Assert.That(Sources(), Is.Empty); Assert.That(Clips(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator InvalidPcmDoesNotAllocateAndDisposedOutputCannotRestart()
        {
            int roots = Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.name == "LivLiv Speech Output");
            Assert.Throws<ArgumentOutOfRangeException>(() => new UnitySpeechOutput(new UnitySpeechOptions { volume = double.NaN }));
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.name == "LivLiv Speech Output"), Is.EqualTo(roots));
            var output = new UnitySpeechOutput();
            try
            {
                foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -1.1f, 1.1f })
                    Assert.Throws<ArgumentException>(() => output.Schedule(new[] { bad }, 24000, output.CurrentTime + 1));
                Assert.That(output.ActivePieceCount, Is.Zero); Assert.That(Sources(), Is.Empty);
                Assert.Throws<ArgumentOutOfRangeException>(() => output.Schedule(new[] { 0f }, 0, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => output.SetVolume(double.NaN));
                output.Dispose(); output.Dispose();
                Assert.Throws<ObjectDisposedException>(() => output.Unlock());
                Assert.Throws<ObjectDisposedException>(() => output.Schedule(new[] { 0f }, 24000, 1));
            }
            finally { output.Dispose(); }
            yield return null;
            Assert.That(Sources(), Is.Empty); Assert.That(Clips(), Is.Empty);
        }
    }
}
