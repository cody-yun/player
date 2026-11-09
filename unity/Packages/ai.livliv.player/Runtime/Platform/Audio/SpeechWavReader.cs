// Byte boundary from src/runtime/speech.ts Incoming.pcm / wavFmt / pcm16.
using System;
using System.IO;

namespace LivLiv.Platform
{
    public sealed class SpeechWavReader : IDisposable
    {
        private readonly int limit;
        private MemoryStream buffered = new MemoryStream();
        private int received, channels, data;
        private bool known, finished;
        public int SampleRate { get; private set; } = 24000;
        public bool NeedsDecoder { get; private set; }
        public SpeechWavReader(int maxBytes = 32 * 1024 * 1024)
        { if (maxBytes < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes)); limit = maxBytes; }

        // Header/sample frames may split at any byte. As upstream, the data chunk length is
        // ignored for streaming WAVs and an incomplete final sample frame is discarded.
        public float[] Push(byte[] bytes, int offset, int count, bool done = false)
        {
            if (buffered == null) throw new ObjectDisposedException(nameof(SpeechWavReader));
            if (finished) throw new InvalidOperationException("WAV input already ended");
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (offset < 0 || count < 0 || offset > bytes.Length - count) throw new ArgumentOutOfRangeException(nameof(count));
            if (count > limit - received) throw new InvalidDataException("Speech download exceeds the encoded byte limit");
            received += count; buffered.Write(bytes, offset, count);
            var b = buffered.GetBuffer(); int length = (int)buffered.Length;
            if (!known && !NeedsDecoder) Parse(b, length, done);
            finished = done;
            if (!known) return Array.Empty<float>();
            int step = 2 * channels, n = (length - data) / step;
            if (n == 0) return Array.Empty<float>();
            var samples = new float[n];
            for (int i = 0; i < n; i++)
            {
                int at = data + i * step;
                samples[i] = (short)(b[at] | b[at + 1] << 8) / 32768f;
            }
            int used = data + n * step, left = length - used;
            Buffer.BlockCopy(b, used, b, 0, left);
            buffered.SetLength(left); buffered.Position = left; data = 0;
            return samples;
        }
        public byte[] DecoderBytes()
        {
            if (buffered == null) throw new ObjectDisposedException(nameof(SpeechWavReader));
            if (!finished || !NeedsDecoder) throw new InvalidOperationException("Only a completed unsupported WAV needs whole-file decoding");
            return buffered.ToArray();
        }
        private void Parse(byte[] b, int length, bool done)
        {
            if (length >= 12 && (!Tag(b, 0, "RIFF") || !Tag(b, 8, "WAVE"))) { NeedsDecoder = true; return; }
            long at = 12; uint rate = 0; int ch = 0; bool ok = false;
            while (length >= 12 && at + 8 <= length)
            {
                int o = (int)at; uint size = U32(b, o + 4);
                if (Tag(b, o, "data"))
                {
                    if (!ok) { NeedsDecoder = true; return; }
                    // The source loops forever on zero channels. Reject this malformed header.
                    if (ch == 0) throw new InvalidDataException("WAV has zero channels");
                    channels = ch; data = o + 8; known = true;
                    if (rate >= 3000 && rate <= 384000) SampleRate = (int)rate;
                    return;
                }
                if (Tag(b, o, "fmt "))
                {
                    if (o + 24 > length) break;
                    if (size < 16) throw new InvalidDataException("WAV fmt chunk is too short");
                    ok = U16(b, o + 8) == 1 && U16(b, o + 22) == 16;
                    ch = U16(b, o + 10); rate = U32(b, o + 12);
                }
                at += 8L + size + (size & 1);
            }
            if (done) NeedsDecoder = true;
        }
        private static bool Tag(byte[] b, int at, string tag) => b[at] == tag[0] && b[at + 1] == tag[1] && b[at + 2] == tag[2] && b[at + 3] == tag[3];
        private static int U16(byte[] b, int at) => b[at] | b[at + 1] << 8;
        private static uint U32(byte[] b, int at) => (uint)(b[at] | b[at + 1] << 8 | b[at + 2] << 16 | b[at + 3] << 24);
        public void Dispose() { buffered?.Dispose(); buffered = null; }
    }
}
