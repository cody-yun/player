// Port of src/runtime/actions.ts. Copyright 2026 LivLiv. Apache-2.0.
// Unity adaptation: dictionary represents Partial<Params>; same curves and ordering.
using System;
using System.Collections.Generic;

namespace LivLiv.Runtime
{
    public sealed class ActionPlayer
    {
        private sealed class Playing { public string id; public double t, output = -1; }
        private readonly List<Playing> playing = new List<Playing>();
        private readonly Dictionary<string, double> off = new Dictionary<string, double>();
        private static readonly Dictionary<string, double> Dur = new Dictionary<string, double>
            { ["nod"] = .95, ["shake"] = 1.05, ["tilt"] = 1.8, ["hop"] = .65, ["shy"] = 2.4 };
        public bool away, auto;
        public double awayW, y;
        public bool Busy => playing.Count > 0 || awayW > 0;

        public void Play(string id)
        {
            if (id == "away") { SetAway(!away); return; }
            if (!Dur.ContainsKey(id)) throw new ArgumentException("Unknown action: " + id, nameof(id));
            foreach (var p in playing) if (p.id == id && p.output < 0) p.output = 0;
            playing.Add(new Playing { id = id });
        }
        public void SetAway(bool on, bool automatic = false) { away = on; auto = on && automatic; }
        public IReadOnlyDictionary<string, double> Update(double dt)
        {
            off.Clear(); y = 0;
            foreach (var p in playing)
            {
                p.t += dt;
                if (p.output >= 0) p.output += dt;
                var t = Math.Min(p.t, Dur[p.id]);
                var e = Params.Smooth(0, .18, t) * (1 - Params.Smooth(Dur[p.id] - .22, Dur[p.id], t));
                var f = p.output < 0 ? 1 : 1 - Params.Smooth(0, .15, p.output);
                void Add(string k, double v) { off[k] = (off.TryGetValue(k, out var old) ? old : 0) + v * f; }
                switch (p.id)
                {
                    case "nod": Add("angleY", -(t < .45 ? 11 * Math.Sin(Math.PI * t / .45) : 6 * Math.Sin(Math.PI * Math.Min(t - .45, .45) / .45)) * e); break;
                    case "shake":
                        var x = 13 * Math.Sin(2 * Math.PI * t / .5) * e;
                        Add("angleX", x); Add("bodyX", x * .15); break;
                    case "tilt": Add("angleZ", 11 * e); Add("angleX", 3 * e); Add("angleY", 2 * e); break;
                    case "hop":
                        y += (t < .2 ? -.025 * Math.Sin(Math.PI * t / .2) : t < .6 ? .04 * Math.Sin(Math.PI * (t - .2) / .4) : 0) * f;
                        Add("angleY", t > .2 && t < .6 ? 5 * Math.Sin(Math.PI * (t - .2) / .4) * e : 0); break;
                    case "shy": Add("angleY", -12 * e); Add("angleX", 9 * e); Add("angleZ", -4 * e); Add("eyeBallX", .7 * e); Add("eyeBallY", -.5 * e); break;
                }
            }
            playing.RemoveAll(p => !(p.t < Dur[p.id] && p.output < .15));
            awayW += ((away ? 1 : 0) - awayW) * Math.Min(1, dt * (away ? 2.2 : 4));
            if (awayW < .002 && !away) awayW = 0;
            return off;
        }
        public Dictionary<string, double> AwayPose(double t) => new Dictionary<string, double>
        {
            ["eyeLOpen"] = 0, ["eyeROpen"] = 0, ["angleY"] = -11, ["angleZ"] = 5, ["angleX"] = 0,
            ["mouthOpen"] = 0, ["smile"] = 0, ["eyeBallX"] = 0, ["eyeBallY"] = 0,
            ["breath"] = .5 + .5 * Math.Sin(t * 1.15)
        };
    }
}
