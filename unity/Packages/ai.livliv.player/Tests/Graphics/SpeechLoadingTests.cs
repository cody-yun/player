using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace LivLiv.Tests
{
    public sealed class SpeechLoadingTests
    {
        private static string Fixture(string name) => Path.GetFullPath(Path.Combine(Application.dataPath, "../Packages/ai.livliv.player/Tests/Graphics/Fixtures/" + name));
        private sealed class Origin : IDisposable
        {
            private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            private readonly CancellationTokenSource cancel = new CancellationTokenSource();
            private readonly List<TcpClient> clients = new List<TcpClient>();
            private readonly byte[] wav, mp3;
            public readonly TaskCompletionSource<bool> release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public readonly string Url;
            public bool tailSent;
            public int missing;
            public Origin()
            {
                wav = File.ReadAllBytes(Fixture("speech-stereo.wav.bytes")); mp3 = File.ReadAllBytes(Fixture("speech-stereo.mp3.bytes"));
                listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port; _ = Accept();
            }
            private async Task Accept()
            {
                try { while (!cancel.IsCancellationRequested) { var c = await listener.AcceptTcpClientAsync(); lock (clients) clients.Add(c); _ = Reply(c); } }
                catch (Exception) when (cancel.IsCancellationRequested) { }
            }
            private async Task Reply(TcpClient client)
            {
                try
                {
                    using (client) using (var stream = client.GetStream()) using (var reader = new StreamReader(stream, Encoding.ASCII, false, 4096, true))
                    {
                        string first = await reader.ReadLineAsync(); if (first == null) return;
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) { }
                        string path = first.Split(' ')[1]; bool compressed = path == "/clip.mp3";
                        if (path == "/missing") missing++;
                        var bytes = compressed ? mp3 : wav;
                        var header = Encoding.ASCII.GetBytes("HTTP/1.1 " + (path == "/missing" ? "404 Missing" : "200 OK") + "\r\nContent-Type: " + (compressed ? "audio/mpeg" : "audio/wav") + "\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
                        await stream.WriteAsync(header, 0, header.Length);
                        if (path == "/stream" || path == "/cut")
                        {
                            // Header plus 21 stereo sample frames, then deliberately stop sending.
                            await stream.WriteAsync(bytes, 0, 128); await stream.FlushAsync();
                            if (path == "/cut") return;
                            await release.Task;
                            cancel.Token.ThrowIfCancellationRequested();
                            await stream.WriteAsync(bytes, 128, bytes.Length - 128); tailSent = true;
                        }
                        else await stream.WriteAsync(bytes, 0, bytes.Length);
                    }
                }
                catch (Exception e) when (e is IOException || e is SocketException || e is ObjectDisposedException || e is OperationCanceledException) { }
                finally { lock (clients) clients.Remove(client); }
            }
            public void Dispose()
            {
                cancel.Cancel(); release.TrySetResult(true); listener.Stop();
                lock (clients) foreach (var c in clients) c.Close();
            }
        }
        private static IEnumerator Run(Task test)
        {
            while (!test.IsCompleted) yield return null;
            if (test.IsFaulted) throw test.Exception.InnerException;
            Assert.That(test.IsCanceled, Is.False);
        }
        private static async Task<T> Await<T>(Task<T> task, string phase = "read")
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline) await Task.Yield();
            Assert.That(task.IsCompleted, Is.True, "speech operation timed out: " + phase); return await task;
        }
        private static async Task<List<float>> Drain(ISpeechIncoming input)
        {
            var result = new List<float>();
            for (float[] x; (x = await Await(input.NextAsync())) != null;) result.AddRange(x);
            return result;
        }
        [UnityTest] public IEnumerator StreamsWavBeforeEndOfHttpBody() => Run(Stream());
        private static async Task Stream()
        {
            using (var origin = new Origin())
            using (var incoming = new UnitySpeechLoader().Open(origin.Url + "/stream", default))
            {
                var first = await Await(incoming.NextAsync());
                Assert.That(first.Length, Is.EqualTo(21)); Assert.That(origin.tailSent, Is.False);
                Assert.That(incoming.SampleRate, Is.EqualTo(24000));
                origin.release.SetResult(true);
                var rest = await Drain(incoming);
                Assert.That(first.Length + rest.Count, Is.EqualTo(8000));
                for (int i = 0; i < first.Length; i++)
                    Assert.That(first[i], Is.EqualTo(Math.Round(.2 * Math.Sin(2 * Math.PI * 330 * i / 24000) * 32767) / 32768).Within(1e-7));
            }
        }
        [UnityTest] public IEnumerator DecodesMp3AndFloatWavAndCopiesByteInput() => Run(Decode());
        private static async Task Decode()
        {
            var loader = new UnitySpeechLoader();
            foreach (string name in new[] { "speech-stereo.wav.bytes", "speech-float.wav.bytes", "speech-stereo.mp3.bytes" })
            {
                var bytes = File.ReadAllBytes(Fixture(name));
                using (var incoming = loader.Open(bytes, default))
                {
                    Array.Clear(bytes, 0, bytes.Length); // Open must own a copy before its first await.
                    var pcm = await Drain(incoming);
                    Assert.That(pcm.Count, Is.GreaterThan(7000), name); Assert.That(incoming.SampleRate, Is.EqualTo(24000), name);
                    Assert.That(Energy(pcm, 330, incoming.SampleRate), Is.GreaterThan(Energy(pcm, 770, incoming.SampleRate) * 100), "first channel only: " + name);
                }
            }
            using (var origin = new Origin()) using (var incoming = loader.Open(origin.Url + "/clip.mp3", default))
                Assert.That((await Drain(incoming)).Count, Is.GreaterThan(7000));
            // Whole-file WAV decoding must not treat a trailing RIFF metadata chunk as PCM.
            var wave = File.ReadAllBytes(Fixture("speech-stereo.wav.bytes"));
            int length = wave.Length; Array.Resize(ref wave, length + 24);
            Array.Copy(Encoding.ASCII.GetBytes("JUNK"), 0, wave, length, 4);
            Array.Copy(BitConverter.GetBytes(16), 0, wave, length + 4, 4);
            Array.Copy(BitConverter.GetBytes(wave.Length - 8), 0, wave, 4, 4);
            using (var incoming = loader.Open(wave, default)) Assert.That((await Drain(incoming)).Count, Is.EqualTo(8000));
            string directory = Path.Combine(Application.temporaryCachePath, "LivLivSpeech");
            Assert.That(!Directory.Exists(directory) || Directory.GetFiles(directory).Length == 0, Is.True, "temporary encoded files released");
        }
        private static double Energy(List<float> x, double hz, int rate)
        {
            double re = 0, im = 0;
            for (int i = 0; i < x.Count; i++) { re += x[i] * Math.Cos(2 * Math.PI * hz * i / rate); im += x[i] * Math.Sin(2 * Math.PI * hz * i / rate); }
            return re * re + im * im;
        }
        [UnityTest] public IEnumerator HttpFailureCancellationAndBudgetsDoNotLeavePendingReads() => Run(Failure());
        private static async Task Failure()
        {
            using (var origin = new Origin())
            {
                var loader = new UnitySpeechLoader(new UnitySpeechLoaderOptions { baseUri = new Uri(origin.Url) });
                using (var incoming = loader.Open("/missing", default))
                { try { await Await(incoming.NextAsync(), "404"); Assert.Fail("404 returned PCM"); } catch (IOException) { } }
                Assert.That(origin.missing, Is.EqualTo(1), "relative URL must use HTTP base URI");
                using (var cancel = new CancellationTokenSource()) using (var incoming = loader.Open(origin.Url + "/stream", cancel.Token))
                {
                    Assert.That((await Await(incoming.NextAsync(), "before cancellation")).Length, Is.GreaterThan(0));
                    var pending = incoming.NextAsync(); cancel.Cancel();
                    try { await Await(pending, "cancellation"); Assert.Fail("pending read survived cancellation"); } catch (OperationCanceledException) { }
                }
                using (var incoming = new UnitySpeechLoader(new UnitySpeechLoaderOptions { maxEncodedBytes = 60 }).Open(origin.Url + "/stream", default))
                { try { await Await(incoming.NextAsync(), "encoded budget"); Assert.Fail("byte limit ignored"); } catch (InvalidDataException) { } }
                using (var incoming = new UnitySpeechLoader(new UnitySpeechLoaderOptions { maxDecodedSamples = 10 }).Open(File.ReadAllBytes(Fixture("speech-stereo.wav.bytes")), default))
                { try { await Await(incoming.NextAsync(), "decoded budget"); Assert.Fail("sample limit ignored"); } catch (InvalidDataException) { } }
            }
        }
        [UnityTest] public IEnumerator ShortWholeClipsAreSilentButStreamedShortPcmIsRetained() => Run(Short());
        private static async Task Short()
        {
            // Byte inputs follow decodeAudioData's >50ms gate. Streamed PCM has no such gate
            // (the previous HTTP test observed just 21 frames before the rest arrived).
            var bytes = File.ReadAllBytes(Fixture("speech-stereo.wav.bytes")); Array.Resize(ref bytes, 44 + 100 * 4);
            Array.Copy(BitConverter.GetBytes(bytes.Length - 8), 0, bytes, 4, 4);
            Array.Copy(BitConverter.GetBytes(bytes.Length - 44), 0, bytes, 40, 4);
            using (var incoming = new UnitySpeechLoader().Open(bytes, default)) Assert.That(await Await(incoming.NextAsync()), Is.Null);
        }
    }
}
