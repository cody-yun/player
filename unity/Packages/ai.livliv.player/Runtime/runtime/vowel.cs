// Port of src/runtime/vowel.ts. Copyright 2026 LivLiv. Apache-2.0.
// Double intermediates, float storage mirror JS Number and Float32Array respectively.
using System;

namespace LivLiv.Runtime
{
    public sealed class VowelCue
    {
        public double center = 15, value;
        private double seen;
        public double Push(float[] power, double hz, double level, double dt)
        {
            if (!(hz > 0)) throw new ArgumentOutOfRangeException(nameof(hz));
            double Sum(double a, double b)
            {
                double s = 1e-12;
                for (int k = (int)Math.Ceiling(a / hz); k * hz < b && k < power.Length; k++) s += power[k];
                return s;
            }
            var back = 10 * Math.Log10(Sum(300, 900) / Sum(1800, 3500));
            var close = 10 * Math.Log10(Sum(250, 650) / Sum(650, 1300));
            double target = 0;
            if (level > .15)
            {
                if (level > .3)
                {
                    seen += dt;
                    var k = Math.Min(1, dt / Math.Min(3, .3 + seen));
                    center += (Params.Clamp(back, 8, 24) - center) * k;
                }
                var round = (back - center) / 10;
                var wide = ((center - back) / 3) * Params.Clamp((close - 8 + 4) / 4, 0, 1);
                target = Params.Clamp(wide > 0 ? wide : -Math.Max(0, round), -1, 1);
            }
            value += (target - value) * Math.Min(1, dt / .035);
            return value;
        }
        // Source intentionally retains the learned center and seen duration.
        public void Reset() { value = 0; }
    }

    public static class Vowel
    {
        private static void Fft(float[] re, float[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { var r = re[i]; re[i] = re[j]; re[j] = r; var v = im[i]; im[i] = im[j]; im[j] = v; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                var a = -2 * Math.PI / len;
                var wr = Math.Cos(a); var wi = Math.Sin(a);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int p = i + k, q = p + len / 2;
                        var tr = re[q] * cr - im[q] * ci;
                        var ti = re[q] * ci + im[q] * cr;
                        re[q] = (float)(re[p] - tr); im[q] = (float)(im[p] - ti);
                        re[p] = (float)(re[p] + tr); im[p] = (float)(im[p] + ti);
                        var c2 = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = c2;
                    }
                }
            }
        }
        private static int JsRound(double x) => (int)Math.Floor(x + .5);
        public static float[] Track(float[] x, double sr, float[] env, double fps, double lead = 0,
            VowelCue cue = null, int from = 0, int? to = null, float[] output = null)
        {
            if (!(sr > 0) || !(fps > 0)) throw new ArgumentOutOfRangeException("Sample rate and fps must be positive");
            cue = cue ?? new VowelCue(); output = output ?? new float[env.Length];
            var hop = sr / fps;
            int size = 1 << JsRound(Math.Log(.0213 * sr, 2));
            var win = new float[size]; var re = new float[size]; var im = new float[size]; var pw = new float[size / 2];
            for (int i = 0; i < size; i++) win[i] = (float)(.42 - .5 * Math.Cos(2 * Math.PI * i / (size - 1)) + .08 * Math.Cos(4 * Math.PI * i / (size - 1)));
            for (int f = from; f < (to ?? env.Length); f++)
            {
                int c = JsRound((f + lead) * hop);
                for (int i = 0; i < size; i++) { int j = c - size / 2 + i; re[i] = (float)((double)(j >= 0 && j < x.Length ? x[j] : 0) * win[i]); im[i] = 0; }
                Fft(re, im);
                for (int k = 0; k < size / 2; k++) pw[k] = (float)((double)re[k] * re[k] + (double)im[k] * im[k]);
                output[f] = (float)cue.Push(pw, sr / size, env[f], 1 / fps);
            }
            return output;
        }
    }
}
