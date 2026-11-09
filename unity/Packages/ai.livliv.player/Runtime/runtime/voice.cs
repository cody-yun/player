// Port of src/runtime/voice.ts: VoiceMeter.read numeric kernel.
// Copyright 2026 LivLiv. Apache-2.0.
// The adapter must provide 650 Hz / Q .55 band-passed time samples and UNFILTERED linear power.
// Browser device lifecycle / WebAudio are deliberately not claimed as implemented here.
using System;

namespace LivLiv.Runtime
{
    public sealed class VoiceMeter
    {
        public double level, gain = 1, vowel;
        private double floor = .004, peak = .05, last;
        private readonly VowelCue cue = new VowelCue();

        // performance.now boundary of upstream read(): first nonzero timestamp uses 1/60 s.
        public double ReadAt(float[] bandPassedSamples,float[] power,double hz,double nowMilliseconds)
        {
            double dt=Math.Min(.1,last!=0?(nowMilliseconds-last)/1000:1d/60);last=nowMilliseconds;
            return Read(bandPassedSamples,power,hz,dt);
        }
        public double Read(float[] bandPassedSamples, float[] power, double hz, double dt)
        {
            if (bandPassedSamples == null || bandPassedSamples.Length == 0) return 0;
            dt = Math.Min(.1, dt);
            double s = 0;
            foreach (var sample in bandPassedSamples) s += (double)sample * sample;
            var rms = Math.Sqrt(s / bandPassedSamples.Length);
            floor = rms < floor ? floor + (rms - floor) * Math.Min(1, dt * 4) : floor + (rms - floor) * dt * .05;
            floor = Math.Max(.0005, floor);
            var gate = floor * 2.2;
            peak = Math.Max(Math.Max(rms, gate * 3), Math.Max(.012, peak * Math.Exp(-dt / 3)));
            var x = Params.Clamp((rms - gate) / (peak - gate), 0, 1);
            var target = Math.Min(1, Math.Pow(x, .8) * 1.1 * gain);
            level += (target - level) * Math.Min(1, dt * (target > level ? 30 : 14));
            if (power != null) vowel = cue.Push(power, hz, level, dt);
            return level;
        }
    }
}
