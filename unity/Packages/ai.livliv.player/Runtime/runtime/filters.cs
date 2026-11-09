// Port of src/runtime/filters.ts. Copyright 2026 LivLiv. Apache-2.0.
// Unity adaptation: explicit clock input; double preserves JavaScript Number arithmetic.
using System;

namespace LivLiv.Runtime
{
    public sealed class OneEuro
    {
        public double minCutoff, beta;
        private double? x;
        private double dx;
        public OneEuro(double minCutoff, double beta) { this.minCutoff = minCutoff; this.beta = beta; }
        public void Reset() { x = null; dx = 0; }
        public double Filter(double v, double dt)
        {
            if (x == null || !(dt > 0)) { x = v; return v; }
            double Alpha(double cutoff) { var r = 2 * Math.PI * cutoff * dt; return r / (r + 1); }
            dx += ((v - x.Value) / dt - dx) * Alpha(1);
            x += (v - x.Value) * Alpha(minCutoff + beta * Math.Abs(dx));
            return x.Value;
        }
    }
}
