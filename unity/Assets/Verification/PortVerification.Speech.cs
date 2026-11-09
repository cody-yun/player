#if !UNITY_WEBGL || UNITY_EDITOR
// Explicit CLI-only synthetic WAV/MP3 decoder/output probe. No hardware capture or UI changes.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Runtime;
using LivLiv.Unity;
using UnityEngine;

public sealed partial class PortVerification
{
    private void StartSpeechSmokeIfRequested()
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i + 1 < args.Length; i++)
            if (args[i] == "-livlivSpeechSmoke")
            {
                if (nativeSmokePath != null) throw new ArgumentException("Run face and speech probes separately");
                Directory.CreateDirectory(args[i + 1]); StartCoroutine(SpeechSmoke(args[i + 1])); return;
            }
    }
    private static byte[] SpeechSmokeAudio(int index)
    {
        if (index == 2) return Resources.Load<TextAsset>("LivLivSpeechMp3").bytes;
        if (index == 3) return Resources.Load<TextAsset>("LivLivSpeechFloat").bytes;
        using (var bytes = new MemoryStream()) using (var writer = new BinaryWriter(bytes))
        {
            const int count = 8400;
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(24000); writer.Write(48000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                double t = i / 24000d;
                double fade = Math.Min(1, Math.Min(i, count - i - 1) / 240d);
                double syllable = .15 + .85 * Math.Pow(Math.Max(0, Math.Sin(t * 30)), 2);
                writer.Write((short)Math.Round(32767 * .25 * fade * syllable * Math.Sin(2 * Math.PI * 400 * t)));
            }
            return bytes.ToArray();
        }
    }
    [Serializable] private sealed class SpeechEvidence
    {
        public string result, error, platform, graphics, unity;
        public string[] hooks, inputs;
        public bool[] stateEvents;
        public int idle, stopped, frames, remainingPieces, remainingClips;
        public double peak, mouth, dspElapsed, outputLatency, scale;
    }
    private IEnumerator SpeechSmoke(string directory)
    {
        var report = new SpeechEvidence { platform = Application.platform.ToString(), graphics = SystemInfo.graphicsDeviceType.ToString(), unity = Application.unityVersion };
        var calls = new List<string>();
        var stateEvents = new List<bool>();
        bool oldPause = AudioListener.pause; AudioListener.pause = false;
        var output = new UnitySpeechOutput(new UnitySpeechOptions { volume = .02 });
        var runtime = new SpeechPlayer(output, new UnitySpeechLoader());
        var speech = Speech.Attach(player, runtime, new LipSyncOptions { motion = 0 });
        var samples = new float[1024];
        runtime.onError = e => report.error = e.ToString();
        runtime.onState = () => stateEvents.Add(runtime.Running);
        speech.onIdle = () => report.idle++;
        double start = output.CurrentTime, deadline = Time.realtimeSinceStartupAsDouble + 10;
        report.outputLatency = output.OutputLatencySeconds;
        report.inputs = new[] { "PCM16 WAV", "MP3", "float WAV" };
        try
        {
            AudioListener.pause = true; runtime.Tick(); yield return null;
            runtime.Tick(); // unchanged state must not repeat the notification
            AudioListener.pause = false; runtime.Tick(); yield return null;
            for (int i = 1; i <= 3; i++)
            {
                string id = i.ToString();
                runtime.Enqueue(SpeechSmokeAudio(i), new ClipHooks { onStart = () => calls.Add(id + ":start"), onEnd = () => calls.Add(id + ":end"), onFail = () => calls.Add(id + ":fail") });
            }
            AudioListener.GetOutputData(samples, 0);
            while (runtime.Busy && Time.realtimeSinceStartupAsDouble < deadline)
            {
                runtime.Tick();
                AudioListener.GetOutputData(samples, 0);
                foreach (float x in samples) report.peak = Math.Max(report.peak, Math.Abs(x));
                report.mouth = Math.Max(report.mouth, player.Renderer.Core.@params.mouthOpen);
                report.scale = ReadEvidence().scale;
                report.frames++;
                yield return null;
            }
            report.dspElapsed = output.CurrentTime - start;
            if (runtime.Busy) report.error = "Timed out waiting for speech output";
            runtime.Enqueue(SpeechSmokeAudio(1), new ClipHooks { onFail = () => report.stopped++ }); runtime.Stop();
            report.remainingPieces = output.ActivePieceCount;
        }
        finally { speech.Dispose(); AudioListener.pause = oldPause; }
        yield return null;
        report.remainingClips = Resources.FindObjectsOfTypeAll<AudioClip>().Count(c => c.name == "LivLiv Speech PCM");
        report.hooks = calls.ToArray();
        report.stateEvents = stateEvents.ToArray();
        var expected = new[] { "1:start", "1:end", "2:start", "2:end", "3:start", "3:end" };
        bool passed = report.error == null && calls.SequenceEqual(expected) && report.idle == 1 && report.stopped == 1
            && report.peak > 1e-5 && report.mouth > .1 && report.scale == 1 && report.remainingPieces == 0 && report.remainingClips == 0
            && stateEvents.SequenceEqual(new[] { false, true, false });
        report.result = passed ? "Passed" : "Failed";
        File.WriteAllText(Path.Combine(directory, "speech-player.json"), JsonUtility.ToJson(report, true));
        Application.Quit(passed ? 0 : 1);
    }
}
#endif
