using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LivLiv.Platform
{
    // Incoming.next/push/poke boundary. Buffered chunks are joined at read time; a failed
    // transfer yields the already-arrived prefix before reporting failure. Cancel drops it.
    public sealed class SpeechIncomingBuffer : ISpeechIncoming
    {
        private readonly object gate = new object();
        private readonly List<float[]> got = new List<float[]>();
        private readonly int limit;
        private readonly CancellationTokenRegistration cancellation;
        private TaskCompletionSource<float[]> wake;
        private int sampleRate = 24000, received;
        private bool ended, cancelled;
        private Exception failure;
        public int SampleRate { get { lock (gate) return sampleRate; } }
        public SpeechIncomingBuffer(CancellationToken token = default, int maxSamples = 16 * 1024 * 1024)
        {
            if (maxSamples < 1) throw new ArgumentOutOfRangeException(nameof(maxSamples));
            limit = maxSamples; cancellation = token.Register(Cancel);
        }
        public void Push(float[] samples, int rate)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (rate < 3000 || rate > 384000) throw new ArgumentOutOfRangeException(nameof(rate));
            TaskCompletionSource<float[]> pending; float[] data;
            lock (gate)
            {
                if (ended || cancelled) return;
                if (samples.Length == 0) return;
                if (samples.Length > limit - received) throw new InvalidDataException("Speech exceeds the decoded sample limit");
                if (received > 0 && sampleRate != rate) throw new InvalidDataException("Speech sample rate changed inside one clip");
                sampleRate = rate; received += samples.Length;
                got.Add(samples);
                pending = wake; wake = null; data = pending == null ? null : Join();
            }
            pending?.TrySetResult(data);
        }
        public Task<float[]> NextAsync()
        {
            lock (gate)
            {
                if (cancelled) return Task.FromCanceled<float[]>(new CancellationToken(true));
                if (got.Count > 0) return Task.FromResult(Join());
                if (ended) return failure == null ? Task.FromResult<float[]>(null) : Task.FromException<float[]>(failure);
                if (wake != null) throw new InvalidOperationException("Only one speech reader may wait at a time");
                wake = new TaskCompletionSource<float[]>(TaskCreationOptions.RunContinuationsAsynchronously);
                return wake.Task;
            }
        }
        public void Finish(Exception error = null)
        {
            TaskCompletionSource<float[]> pending;
            lock (gate) { if (ended || cancelled) return; ended = true; failure = error; pending = wake; wake = null; }
            if (error == null) pending?.TrySetResult(null); else pending?.TrySetException(error);
        }
        private float[] Join()
        {
            if (got.Count == 1) { var only = got[0]; got.Clear(); return only; }
            int n = 0; foreach (var x in got) n += x.Length;
            var result = new float[n]; int at = 0;
            foreach (var x in got) { Array.Copy(x, 0, result, at, x.Length); at += x.Length; }
            got.Clear(); return result;
        }
        private void Cancel()
        {
            TaskCompletionSource<float[]> pending;
            lock (gate) { cancelled = true; got.Clear(); pending = wake; wake = null; }
            pending?.TrySetCanceled();
        }
        public void Dispose() { Cancel(); cancellation.Dispose(); }
    }
}
