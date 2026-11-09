// Port of src/runtime/speech.ts. Copyright 2026 LivLiv. Apache-2.0.
// Web Audio, fetch and browser timers are host boundaries; Tick replaces the ticker,
// onended/retire timers and suspended-context watchdog. Call it even when drawing is paused.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;

namespace LivLiv.Runtime
{
    public sealed class ClipHooks
    {
        public Action onStart, onEnd, onFail;
        internal ClipHooks Copy() => new ClipHooks { onStart = onStart, onEnd = onEnd, onFail = onFail };
    }

    public sealed class SpeechPlayer : IDisposable
    {
        public const int Fps = 100, Lead = 3, SettleFrames = 10 + Lead + 2;
        private sealed class Piece
        {
            public double at;
            public int from, n;
            public ISpeechPiece source;
        }
        private sealed class Clip
        {
            public readonly List<Piece> pieces = new List<Piece>();
            public readonly Grow samples = new Grow();
            public int rate, envDone, vowelDone;
            public float[] env = Array.Empty<float>(), vowel = Array.Empty<float>();
            public bool complete, started;
            public double retireAt = double.PositiveInfinity;
            public ClipHooks hooks;
        }
        private sealed class Grow
        {
            private float[] data = new float[1 << 14];
            public int Count { get; private set; }
            public void Push(float[] x)
            {
                if (Count + x.Length > data.Length) Array.Resize(ref data, Math.Max(data.Length * 2, Count + x.Length));
                Array.Copy(x, 0, data, Count, x.Length); Count += x.Length;
            }
            public float[] View() { var result = new float[Count]; Array.Copy(data, result, Count); return result; }
        }

        private readonly ISpeechOutput output;
        private readonly ISpeechLoader loader;
        private readonly VowelCue cue = new VowelCue();
        private readonly List<Clip> clips = new List<Clip>();
        private readonly HashSet<ClipHooks> owed = new HashSet<ClipHooks>();
        private CancellationTokenSource loads = new CancellationTokenSource();
        private Task order = Task.CompletedTask;
        private int generation, pending;
        private double tail, watchdogAt = double.PositiveInfinity, watchdogTime;
        private bool disposed, lastRunning;
        public Action onIdle;
        // Source onState counterpart: output state, independent of queue/busy/idle.
        // Main-thread Tick delivers platform events; Dispose delivers final closed state inline.
        public Action onState;
        // Reports adapter/user callback faults without preventing the rest of the queue settling.
        public Action<Exception> onError;

        public SpeechPlayer(ISpeechOutput output, ISpeechLoader loader)
        {
            this.output = output ?? throw new ArgumentNullException(nameof(output));
            this.loader = loader ?? throw new ArgumentNullException(nameof(loader));
            lastRunning = output.Running;
        }
        public bool Busy => pending > 0 || clips.Count > 0;
        public bool Running => !disposed && output.Running;
        public void Unlock() { ThrowIfDisposed(); output.Unlock(); }
        public void SetVolume(double volume) { ThrowIfDisposed(); output.SetVolume(volume); }

        public void Enqueue(string url, ClipHooks hooks = null)
        {
            ThrowIfDisposed();
            if (url == null) throw new ArgumentNullException(nameof(url));
            Enqueue(token => loader.Open(url, token), hooks);
        }
        public void Enqueue(byte[] encodedAudio, ClipHooks hooks = null)
        {
            ThrowIfDisposed();
            if (encodedAudio == null) throw new ArgumentNullException(nameof(encodedAudio));
            if (!(loader is ISpeechBytesLoader bytesLoader)) throw new NotSupportedException("This speech loader does not accept encoded bytes");
            Enqueue(token => bytesLoader.Open(encodedAudio, token), hooks);
        }
        private void Enqueue(Func<CancellationToken, ISpeechIncoming> open, ClipHooks hooks)
        {
            output.Unlock();
            var h = hooks?.Copy();
            if (h != null) owed.Add(h);
            int gen = generation;
            ISpeechIncoming incoming;
            try { incoming = open(loads.Token); }
            catch (Exception ex) { Settle(h, false); Report(ex); Check(); return; }
            pending++;
            // Publish the gate before consuming: a cached empty input can finish synchronously
            // and its idle/fail callback may enqueue another sentence.
            var previous = order;
            var completion = new TaskCompletionSource<bool>();
            order = completion.Task;
            _ = Complete(Consume(previous, incoming, gen, h), completion);
        }

        private async Task Complete(Task work, TaskCompletionSource<bool> completion)
        {
            try { await work; }
            catch (Exception ex) { Report(ex); }
            finally { completion.TrySetResult(true); }
        }

        private async Task Consume(Task previous, ISpeechIncoming incoming, int gen, ClipHooks hooks)
        {
            Clip clip = null;
            try
            {
                await previous;
                while (gen == generation)
                {
                    float[] x = await incoming.NextAsync();
                    if (gen != generation || x == null) break;
                    if (x.Length == 0) continue;
                    clip = Play(clip, x, incoming.SampleRate, hooks);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { Report(ex); }
            finally
            {
                try { incoming.Dispose(); } catch (Exception ex) { Report(ex); }
                if (gen == generation)
                {
                    pending--;
                    if (clip != null)
                    {
                        clip.complete = true;
                        Update(clip);
                    }
                    else Settle(hooks, false);
                    Check();
                }
            }
        }

        private Clip Play(Clip clip, float[] x, int rate, ClipHooks hooks)
        {
            if (rate < 3000 || rate > 384000) throw new ArgumentOutOfRangeException(nameof(rate));
            var c = clip ?? new Clip { rate = rate, hooks = hooks };
            double at = Math.Max(output.CurrentTime + .04, tail);
            var source = output.Schedule(x, c.rate, at);
            if (clip == null) clips.Add(c);
            c.pieces.Add(new Piece { at = at, from = c.samples.Count, n = x.Length, source = source });
            c.samples.Push(x);
            tail = at + (double)x.Length / c.rate;
            Update(c);
            watchdogTime = output.CurrentTime;
            watchdogAt = output.WallTime + tail - watchdogTime + 2;
            return c;
        }

        public double Read() => At(false);
        public double Vowel() => At(true);
        private double At(bool vowel)
        {
            if (disposed) return 0;
            double time = output.HeardTime;
            foreach (var c in clips)
                foreach (var p in c.pieces)
                    if (time >= p.at && time < p.at + (double)p.n / c.rate)
                    {
                        double f = ((double)p.from / c.rate + time - p.at) * Fps;
                        int i = (int)Math.Floor(f); double k = f - i;
                        var track = vowel ? c.vowel : c.env;
                        double a = i < track.Length ? track[i] : 0, b = i + 1 < track.Length ? track[i + 1] : 0;
                        return a + (b - a) * k;
                    }
            return 0;
        }

        private void Update(Clip c)
        {
            var x = c.samples.View(); var e = Envelope(x, c.rate); int n = e.Length;
            int done = c.complete ? n : Math.Max(c.envDone, n - SettleFrames);
            var env = new float[n]; var vowel = new float[n];
            Array.Copy(c.env, env, c.envDone); Array.Copy(e, c.envDone, env, c.envDone, n - c.envDone);
            Array.Copy(c.vowel, vowel, c.vowelDone);
            Runtime.Vowel.Track(x, c.rate, env, Fps, Lead, cue, c.vowelDone, done, vowel);
            c.env = env; c.vowel = vowel; c.envDone = c.vowelDone = done;
        }

        // Main-thread host pump. Delayed frames still emit onStart then exactly one terminal hook.
        public void Tick()
        {
            if (disposed) return;
            int gen = generation;
            PollState();
            if (disposed || gen != generation) return;
            double time = output.HeardTime;
            foreach (var c in clips.ToArray())
            {
                var first = c.pieces[0]; var last = c.pieces[c.pieces.Count - 1];
                if (c.hooks != null && owed.Contains(c.hooks))
                {
                    if (!c.started && time >= first.at) Begin(c);
                    if (gen != generation) return;
                    if (c.started && c.complete && time >= last.at + (double)last.n / c.rate) Settle(c.hooks, true);
                    if (gen != generation) return;
                }
                Retire(c);
                if (gen != generation) return;
            }
            if (output.WallTime >= watchdogAt)
            {
                watchdogAt = double.PositiveInfinity;
                if (output.CurrentTime - watchdogTime < .1 && clips.Count > 0) { Stop(); Check(); }
            }
        }

        private void PollState()
        {
            int count;
            try
            {
                if (output is ISpeechStateOutput events) count = Math.Max(0, events.ConsumeStateChanges());
                else { bool running = output.Running; count = running != lastRunning ? 1 : 0; lastRunning = running; }
            }
            catch (Exception ex) { Report(ex); return; }
            for (int i = 0; i < count && !disposed; i++) Invoke(onState);
        }

        private void Retire(Clip c)
        {
            if (!c.complete || c.pieces.Exists(p => !p.source.Ended)) return;
            if (double.IsPositiveInfinity(c.retireAt))
                c.retireAt = output.WallTime + Math.Min(.4, Math.Max(0, output.CurrentTime - output.HeardTime));
            if (output.WallTime < c.retireAt || !clips.Remove(c)) return;
            foreach (var piece in c.pieces) Release(piece.source);
            int gen = generation;
            if (c.hooks != null && owed.Contains(c.hooks))
            {
                if (!c.started) Begin(c);
                if (gen == generation) Settle(c.hooks, true);
            }
            if (gen == generation) Check();
        }
        private void Begin(Clip c) { c.started = true; Invoke(c.hooks?.onStart); }
        private void Settle(ClipHooks hooks, bool heard)
        {
            if (hooks != null && owed.Remove(hooks)) Invoke(heard ? hooks.onEnd : hooks.onFail);
        }
        private void Check() { if (!Busy) Invoke(onIdle); }

        public void Stop()
        {
            if (disposed) return;
            generation++;
            var oldLoads = loads; loads = new CancellationTokenSource();
            pending = 0; tail = 0; watchdogAt = double.PositiveInfinity;
            // New speech must not wait for a cancelled download that ignores its token.
            order = Task.CompletedTask;
            var oldClips = clips.ToArray(); clips.Clear();
            var oldOwed = new List<ClipHooks>(owed); owed.Clear();
            try { oldLoads.Cancel(); } catch (Exception ex) { Report(ex); }
            oldLoads.Dispose();
            foreach (var c in oldClips) foreach (var p in c.pieces) Release(p.source);
            foreach (var h in oldOwed) Invoke(h.onFail);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            bool notifyClosed = true;
            try { if (output is ISpeechStateOutput states) notifyClosed = states.HasOpenContext; }
            catch (Exception ex) { Report(ex); }
            // Block callback reentrancy from scheduling new work during destruction.
            onIdle = null;
            generation++;
            var oldOwed = new List<ClipHooks>(owed); owed.Clear();
            var oldClips = clips.ToArray(); clips.Clear(); pending = 0;
            try { loads.Cancel(); } catch (Exception ex) { Report(ex); }
            loads.Dispose();
            foreach (var c in oldClips) foreach (var p in c.pieces) Release(p.source);
            foreach (var h in oldOwed) Invoke(h.onFail);
            Release(output);
            // Unity pumps are destroyed with the output. Report the final closed state here
            // instead of retaining a destroyed owner for the browser's later close event.
            var closed = onState; onState = null;
            if (notifyClosed) Invoke(closed);
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(SpeechPlayer)); }
        private void Release(IDisposable resource) { try { resource.Dispose(); } catch (Exception ex) { Report(ex); } }
        private void Invoke(Action callback) { try { callback?.Invoke(); } catch (Exception ex) { Report(ex); } }
        private void Report(Exception error) { try { onError?.Invoke(error); } catch { /* Diagnostics cannot strand queued clips. */ } }

        // Float stores correspond to the source Float32Arrays; intermediates remain doubles.
        public static float[] Envelope(float[] x, double sampleRate)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (!(sampleRate >= 3000 && sampleRate <= 384000)) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            int hop = Math.Max(1, (int)Math.Floor(sampleRate / Fps + .5));
            int n = (int)Math.Ceiling((double)x.Length / hop);
            double lp = 1 - Math.Exp(-2 * Math.PI * 1500 / sampleRate), hp = 1 - Math.Exp(-2 * Math.PI * 150 / sampleRate);
            var sum = new float[n]; double a = 0, b = 0;
            for (int i = 0; i < x.Length; i++)
            {
                a += (x[i] - a) * lp; b += (a - b) * hp;
                double v = a - b; sum[i / hop] = (float)(sum[i / hop] + v * v);
            }
            var e = new float[n]; double max = 0;
            for (int i = 0; i < n; i++)
            {
                e[i] = (float)Math.Sqrt(((double)sum[i] + (i + 1 < n ? sum[i + 1] : 0)) / (2 * hop));
                max = Math.Max(max, e[i]);
            }
            if (max == 0) return e;
            var voiced = new List<float>(); foreach (float value in e) if (value > max * .05) voiced.Add(value);
            voiced.Sort(); double reference = voiced.Count > 0 ? voiced[(int)Math.Floor(voiced.Count * .9)] : max;
            double gate = reference * .1;
            var level = new float[n];
            for (int i = 0; i < n; i++) level[i] = (float)Math.Pow(Math.Min(1, Math.Max(0, (e[i] - gate) / Math.Max(reference - gate, 1e-6))), .75);
            var result = new float[n]; double s = 0;
            for (int i = 0; i < n; i++)
            {
                double avg = 0;
                for (int j = i - 10; j <= i + 10; j++) avg += level[Math.Min(n - 1, Math.Max(0, j))];
                avg /= 21;
                double openness = Math.Min(1, Math.Max(0, level[i] * .6 + (level[i] - avg) * 1.2));
                s += (openness - s) * (openness > s ? .6 : .45);
                if (i >= Lead) result[i - Lead] = (float)s;
            }
            return result;
        }
    }
}
