using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using UnityEngine;
using UnityEngine.Networking;

namespace LivLiv.Unity
{
    public sealed class UnitySpeechLoaderOptions
    {
        public Uri baseUri;
        public int maxEncodedBytes = 32 * 1024 * 1024;
        public int maxDecodedSamples = 16 * 1024 * 1024;
        public int timeoutSeconds = 60;
    }

    // Incoming.load/decode/pcm native boundary. HTTP WAV PCM16 streams as it arrives;
    // compressed/other audio is decoded whole. Calls belong on Unity's main thread.
    public sealed class UnitySpeechLoader : ISpeechBytesLoader
    {
        private readonly Uri baseUri;
        private readonly int maxBytes, maxSamples, timeout;
        public UnitySpeechLoader(UnitySpeechLoaderOptions options = null)
        {
            options = options ?? new UnitySpeechLoaderOptions();
            if (options.maxEncodedBytes < 1 || options.maxDecodedSamples < 1 || options.timeoutSeconds < 1)
                throw new ArgumentOutOfRangeException(nameof(options));
            if (options.baseUri != null && !options.baseUri.IsAbsoluteUri) throw new ArgumentException("Speech base URI must be absolute", nameof(options));
            baseUri = options.baseUri; maxBytes = options.maxEncodedBytes; maxSamples = options.maxDecodedSamples; timeout = options.timeoutSeconds;
        }
        public ISpeechIncoming Open(string url, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Uri uri;
            bool valid = baseUri != null ? Uri.TryCreate(baseUri, url, out uri) : Uri.TryCreate(url, UriKind.Absolute, out uri);
            if (!valid) throw new ArgumentException("Speech URL must be absolute or have a base URI", nameof(url));
            if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "file") throw new ArgumentException("Unsupported speech URL scheme", nameof(url));
            return new Incoming(this, uri, null, token);
        }
        public ISpeechIncoming Open(byte[] encodedAudio, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (encodedAudio == null) throw new ArgumentNullException(nameof(encodedAudio));
            if (encodedAudio.Length > maxBytes) throw new InvalidDataException("Speech exceeds the encoded byte limit");
            // Mirrors Blob([ArrayBuffer]): later caller mutations cannot alter queued audio.
            return new Incoming(this, null, (byte[])encodedAudio.Clone(), token);
        }

        private sealed class Incoming : ISpeechIncoming
        {
            private readonly UnitySpeechLoader owner;
            private readonly CancellationTokenSource cancel;
            private readonly SpeechIncomingBuffer buffer;
            private UnityWebRequest request;
            private bool disposed, loadFinished;
            public int SampleRate => buffer.SampleRate;
            public Incoming(UnitySpeechLoader owner, Uri uri, byte[] bytes, CancellationToken token)
            {
                this.owner = owner;
                cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
                buffer = new SpeechIncomingBuffer(cancel.Token, owner.maxSamples);
                _ = Load(uri, bytes);
            }
            public Task<float[]> NextAsync() => buffer.NextAsync();
            private async Task Load(Uri uri, byte[] bytes)
            {
                try
                {
                    // Keep completion/disposal out of the constructor and all Unity APIs on
                    // the captured Unity synchronization context.
                    await Task.Yield();
                    cancel.Token.ThrowIfCancellationRequested();
                    if (bytes != null) await DecodeWhole(bytes);
                    else
                    {
                        using (var body = new Body(this))
                        using (var http = new UnityWebRequest(uri.AbsoluteUri, UnityWebRequest.kHttpVerbGET, body, null))
                        {
                            request = http; http.disposeDownloadHandlerOnDispose = false; http.timeout = owner.timeout;
                            var op = http.SendWebRequest();
                            try
                            {
                                while (!op.isDone)
                                {
                                    cancel.Token.ThrowIfCancellationRequested();
                                    if (body.Failure != null) throw body.Failure;
                                    await Task.Yield();
                                }
                                cancel.Token.ThrowIfCancellationRequested();
                                if (body.Failure != null) throw body.Failure;
                                if (http.responseCode >= 400) throw new IOException("Speech HTTP " + http.responseCode);
                                if (http.result != UnityWebRequest.Result.Success) throw new IOException("Speech download failed: " + http.error);
                                await body.Complete();
                            }
                            finally { if (!op.isDone) http.Abort(); request = null; }
                        }
                    }
                    buffer.Finish();
                }
                catch (OperationCanceledException) { buffer.Dispose(); }
                catch (Exception error) { buffer.Finish(error); }
                finally
                {
                    // An exception inside the polling loop must abort before disposal too.
                    if (request != null) { request.Dispose(); request = null; }
                    loadFinished = true;
                    if (disposed) cancel.Dispose();
                }
            }
            private async Task DecodeWhole(byte[] bytes)
            {
                cancel.Token.ThrowIfCancellationRequested();
                // Whole-file decoding must respect RIFF data lengths and trailing metadata.
                // The streaming parser intentionally ignores those lengths and is not suitable here.
                var audio = await UnitySpeechDecoder.Decode(bytes, owner.maxSamples, cancel.Token);
                cancel.Token.ThrowIfCancellationRequested();
                if ((double)audio.samples.Length / audio.rate > .05) buffer.Push(audio.samples, audio.rate);
            }
            public void Dispose()
            {
                if (disposed) return; disposed = true;
                cancel.Cancel(); buffer.Dispose();
                if (request != null && !request.isDone) request.Abort();
                if (loadFinished) cancel.Dispose();
            }

            private sealed class Body : DownloadHandlerScript
            {
                private readonly Incoming incoming;
                private SpeechWavReader wav;
                private MemoryStream whole;
                private int received;
                public Exception Failure { get; private set; }
                public Body(Incoming incoming) : base(new byte[64 * 1024]) { this.incoming = incoming; }
                protected override bool ReceiveData(byte[] bytes, int count)
                {
                    try
                    {
                        incoming.cancel.Token.ThrowIfCancellationRequested();
                        if (incoming.request.responseCode >= 400) return true;
                        if (count > incoming.owner.maxBytes - received) throw new InvalidDataException("Speech exceeds the encoded byte limit");
                        received += count;
                        if (wav == null && whole == null)
                        {
                            string mime = incoming.request.GetResponseHeader("Content-Type") ?? "";
                            if (mime.Contains("wav")) wav = new SpeechWavReader(incoming.owner.maxBytes);
                            else whole = new MemoryStream();
                        }
                        if (wav != null) incoming.buffer.Push(wav.Push(bytes, 0, count), wav.SampleRate);
                        else whole.Write(bytes, 0, count);
                        return true;
                    }
                    catch (Exception ex) { Failure = ex; return false; }
                }
                public async Task Complete()
                {
                    if (wav != null)
                    {
                        incoming.buffer.Push(wav.Push(Array.Empty<byte>(), 0, 0, true), wav.SampleRate);
                        if (wav.NeedsDecoder) await incoming.DecodeWhole(wav.DecoderBytes());
                    }
                    else await incoming.DecodeWhole(whole?.ToArray() ?? Array.Empty<byte>());
                }
                public override void Dispose() { wav?.Dispose(); whole?.Dispose(); base.Dispose(); }
            }
        }
    }

    internal static class UnitySpeechDecoder
    {
        internal sealed class Audio { public float[] samples; public int rate; }
        internal static async Task<Audio> Decode(byte[] bytes, int maxSamples, CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return await Task.FromException<Audio>(new PlatformNotSupportedException("LivLiv: WebGL encoded speech requires decodeAudioData"));
#else
            token.ThrowIfCancellationRequested();
            AudioType type; string extension;
            bool Tag(int at, string tag)
            { if (at + tag.Length > bytes.Length) return false; for (int i = 0; i < tag.Length; i++) if (bytes[at + i] != tag[i]) return false; return true; }
            if (Tag(0, "RIFF") && Tag(8, "WAVE")) { type = AudioType.WAV; extension = ".wav"; }
            else if (Tag(0, "OggS")) { type = AudioType.OGGVORBIS; extension = ".ogg"; }
            else if (Tag(0, "ID3") || (bytes.Length > 1 && bytes[0] == 255 && (bytes[1] & 224) == 224)) { type = AudioType.MPEG; extension = ".mp3"; }
            else throw new InvalidDataException("Unsupported or invalid encoded speech");
            string directory = Path.Combine(Application.temporaryCachePath, "LivLivSpeech"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + extension);
            AudioClip clip = null;
            try
            {
                File.WriteAllBytes(path, bytes);
                using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, type))
                {
                    ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = false;
                    var op = request.SendWebRequest();
                    try
                    {
                        while (!op.isDone) { token.ThrowIfCancellationRequested(); await Task.Yield(); }
                        // Capture an already-created clip before checking late cancellation so
                        // the finally block still owns and releases it.
                        if (request.result == UnityWebRequest.Result.Success) clip = DownloadHandlerAudioClip.GetContent(request);
                        token.ThrowIfCancellationRequested();
                        if (request.result != UnityWebRequest.Result.Success) throw new InvalidDataException("Speech decoder failed: " + request.error);
                        if (clip == null || clip.channels < 1 || clip.frequency < 3000 || clip.frequency > 384000)
                            throw new InvalidDataException("Speech decoder returned an invalid clip");
                        long count = (long)clip.samples * clip.channels;
                        if (count > maxSamples) throw new InvalidDataException("Speech exceeds the decoded sample limit");
                        var all = new float[(int)count];
                        if (all.Length > 0 && !clip.GetData(all, 0)) throw new InvalidDataException("Speech decoder PCM is not readable");
                        var first = new float[clip.samples];
                        for (int i = 0; i < first.Length; i++) first[i] = all[i * clip.channels];
                        return new Audio { samples = first, rate = clip.frequency };
                    }
                    finally { if (!op.isDone) request.Abort(); }
                }
            }
            finally { if (clip != null) TextureDecoder.Destroy(clip); if (File.Exists(path)) File.Delete(path); }
#endif
        }
    }
}
