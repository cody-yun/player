// Port of src/runtime/tracking.ts: read/apply/calibration/smoothing/mirror and face-loss clock.
// Copyright 2026 LivLiv. Apache-2.0.
// Adaptation: caller supplies FaceFrame and monotonic milliseconds. Camera/inference, device
// storage and illumination diagnostics belong to adapters; calibration policy stays source-mapped.
using System;
using System.Collections.Generic;
using System.Linq;
using LivLiv.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace LivLiv.Runtime
{
    [Serializable]
    public struct FacePose { public double x, y, z, px, py, pz, bl, br; }
    [Serializable]
    public struct FacePosition { public double x, y, z; }

    public sealed class FaceTracker
    {
        public const double LostAfter = .4;
        public readonly Dictionary<string, double> target = new Dictionary<string, double>();
        public double mouthGain = 1, exprGain = 1, follow = .3;
        public bool mirror { get; private set; } = true;
        public FacePose zero;
        public FacePosition raw, pos;
        public bool calibrated { get; private set; }
        public string blinkSync = "turn";
        public double lostFor { get; private set; } = double.PositiveInfinity;
        public string calibId;
        private readonly ICalibrationStore storage;
        private readonly Func<double> unixMilliseconds;
        public FaceTracker(ICalibrationStore storage=null,Func<double> unixMilliseconds=null)
        {this.storage=storage;this.unixMilliseconds=unixMilliseconds??(()=>DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());}
        private bool posKnown;
        private double seenAt = -1;
        private readonly List<FacePose> recent = new List<FacePose>();
        private readonly Dictionary<string, OneEuro> f = new Dictionary<string, OneEuro>
        {
            ["x"] = new OneEuro(.5, .05), ["y"] = new OneEuro(.5, .05), ["z"] = new OneEuro(.5, .05),
            ["gx"] = new OneEuro(1.5, .5), ["gy"] = new OneEuro(1.5, .5), ["brow"] = new OneEuro(1, .5),
            ["px"] = new OneEuro(.6, .08), ["py"] = new OneEuro(.6, .08), ["pz"] = new OneEuro(.6, .08)
        };
        public void Calibrate()
        {
            if (recent.Count == 0) return;
            SetZero(recent.Skip(Math.Max(0, recent.Count - 10)).ToList());SaveCalibration();
        }
        public void SetSmoothing(double s)
        {
            var c = s <= .5 ? .5 * Math.Pow(6, 1 - 2 * s) : .5 / Math.Pow(10d / 3, 2 * s - 1);
            foreach (var key in new[] { "x", "y", "z" }) { f[key].minCutoff = c; f[key].beta = .05 * Math.Pow(4, 1 - 2 * s); }
        }
        public void SetMirror(bool on)
        {
            if (on == mirror) return;
            mirror = on;
            zero.x = -zero.x; zero.z = -zero.z; zero.px = -zero.px;
            for (int i = 0; i < recent.Count; i++)
            {
                var p = recent[i]; p.x = -p.x; p.z = -p.z; p.px = -p.px; recent[i] = p;
            }
            f["x"].Reset(); f["z"].Reset(); f["gx"].Reset(); f["px"].Reset();
        }
        // Null means no new detector result. A frame with neither m nor b means no face.
        // A false return asks the host to hand parameters back to idle; do not apply stale targets then.
        public bool Poll(FaceFrame frame, double now)
        {
            if (frame != null) Read(frame, now);
            lostFor = seenAt < 0 ? double.PositiveInfinity : (now - seenAt) / 1000;
            return lostFor < LostAfter;
        }
        private static double Deg(double r) => r * 180 / Math.PI;
        private static double Hypot(double x, double y) => Math.Sqrt(x * x + y * y);
        private static double Sd(IEnumerable<double> values)
        {
            var xs = values.ToArray(); var mean = xs.Average();
            return Math.Sqrt(xs.Sum(v => (v - mean) * (v - mean)) / xs.Length);
        }
        private static double Eye(double blink, double rest)
        {
            var open = Params.Clamp(rest + .06, .15, .5);
            var shut = Math.Min(.95, Math.Max(.65, open + .4));
            return Params.Clamp(1 - (blink - open) / (shut - open), 0, 1);
        }
        private void Read(FaceFrame frame, double now)
        {
            if (frame.m == null && frame.b == null) return;
            var dt = seenAt < 0 ? 0 : (now - seenAt) / 1000;
            if (dt == 0 || dt > LostAfter) foreach (var filter in f.Values) filter.Reset();
            seenAt = now;
            var b = frame.b;
            double G(string n) { int i = Array.IndexOf(Trace.BS, n); return b != null && i < b.Length ? b[i] : 0; }
            var m = frame.m;
            if (m != null)
            {
                var yaw = Math.Atan2(-m[2], Hypot(m[6], m[10]));
                var pitch = Math.Atan2(m[6], m[10]);
                var roll = Math.Atan2(m[1], m[0]);
                var s = mirror ? -1 : 1;
                raw = new FacePosition { x = s * Deg(yaw) * 1.3, y = -Deg(pitch) * 1.3 - 4, z = s * Deg(roll) };
                var px = s * m[12]; var py = m[13]; var pz = m[14];
                recent.Add(new FacePose { x = raw.x, y = raw.y, z = raw.z, px = px, py = py, pz = pz, bl = G("eyeBlinkLeft"), br = G("eyeBlinkRight") });
                if (recent.Count > 30) recent.RemoveAt(0);
                if ((!calibrated || !posKnown) && recent.Count >= 20)
                {
                    var r = recent.Skip(recent.Count - 20).ToList();
                    if (Sd(r.Select(p => p.x)) < 1.5 && Sd(r.Select(p => p.y)) < 1.5)
                    {
                        if (calibrated)
                        {
                            var z = zero; SetZero(r);
                            zero.x = z.x; zero.y = z.y; zero.z = z.z; zero.bl = z.bl; zero.br = z.br;
                        }
                        else SetZero(r);
                        SaveCalibration();
                    }
                }
                if (posKnown) pos = new FacePosition { x = f["px"].Filter(px, dt) - zero.px, y = f["py"].Filter(py, dt) - zero.py, z = f["pz"].Filter(pz, dt) - zero.pz };
                var x = f["x"].Filter(raw.x, dt) - zero.x;
                target["angleX"] = Params.Clamp(x, -30, 30);
                target["angleY"] = Params.Clamp(f["y"].Filter(raw.y, dt) - zero.y, -30, 30);
                target["angleZ"] = Params.Clamp(zero.z - f["z"].Filter(raw.z, dt), -30, 30);
                target["bodyX"] = Params.Clamp(x * .2 + pos.x * .25 * follow, -10, 10);
            }
            if (b != null)
            {
                var uL = Eye(G("eyeBlinkLeft"), zero.bl); var uR = Eye(G("eyeBlinkRight"), zero.br);
                var l = mirror ? uR : uL; var r = mirror ? uL : uR;
                var turned = Params.Smooth(9, 15, Math.Abs(raw.x - zero.x));
                var k = blinkSync == "always" ? 1 : blinkSync == "turn" ? turned : 0;
                if (k > 0)
                {
                    var avg = (l + r) / 2;
                    var both = avg + (Math.Max(l, r) - avg) * (blinkSync == "always" ? turned : 1);
                    l += (both - l) * k; r += (both - r) * k;
                }
                target["eyeLOpen"] = l; target["eyeROpen"] = r;
                var landmarks = frame.l;
                double D(int i, int j) => landmarks != null ? Hypot(landmarks[2 * i] - landmarks[2 * j], landmarks[2 * i + 1] - landmarks[2 * j + 1]) : 0;
                var lips = landmarks != null ? Params.Clamp((D(1, 2) / Math.Max(1e-3, D(0, 3)) - .015) * 7, 0, 1) : 0;
                target["mouthOpen"] = Params.Clamp(Math.Max((G("jawOpen") - .04) * 2.2, lips) * mouthGain, 0, 1);
                var left = (G("eyeLookOutLeft") + G("eyeLookInRight")) / 2;
                var right = (G("eyeLookInLeft") + G("eyeLookOutRight")) / 2;
                var up = (G("eyeLookUpLeft") + G("eyeLookUpRight")) / 2;
                var down = (G("eyeLookDownLeft") + G("eyeLookDownRight")) / 2;
                target["eyeBallX"] = Params.Clamp(f["gx"].Filter((right - left) * 2.2 * (mirror ? 1 : -1), dt), -1, 1);
                target["eyeBallY"] = Params.Clamp(f["gy"].Filter((up - down) * 2.2, dt), -1, 1);
                var browUp = G("browInnerUp") * .6 + (G("browOuterUpLeft") + G("browOuterUpRight")) * .2;
                var browDown = (G("browDownLeft") + G("browDownRight")) / 2;
                target["browLY"] = target["browRY"] = Params.Clamp(f["brow"].Filter((browUp - browDown * .8) * 2.4, dt), -1, 1);
                target["smile"] = Params.Clamp(((G("mouthSmileLeft") + G("mouthSmileRight")) / 2 - .35 / exprGain) * 2.5 * exprGain, 0, 1);
            }
        }
        private void SetZero(List<FacePose> r)
        {
            double Med(Func<FacePose, double> key) { var a = r.Select(key).OrderBy(v => v).ToArray(); return a[a.Length >> 1]; }
            zero = new FacePose { x = Med(p => p.x), y = Med(p => p.y), z = Med(p => p.z), px = Med(p => p.px), py = Med(p => p.py), pz = Med(p => p.pz),
                bl = Params.Clamp(Med(p => p.bl), 0, .5), br = Params.Clamp(Med(p => p.br), 0, .5) };
            calibrated = posKnown = true;
            f["px"].Reset(); f["py"].Reset(); f["pz"].Reset();
        }
        // Source save/restore: mirrored coordinates are canonical on disk; no expiration is applied.
        private void SaveCalibration()
        {
            if(string.IsNullOrEmpty(calibId)||storage==null)return;
            double s=mirror?1:-1;var z=zero;
            try
            {
                JToken Number(double n)=>double.IsNaN(n)||double.IsInfinity(n)?JValue.CreateNull():new JValue(n);
                var c=new JObject{["x"]=Number(s*z.x),["y"]=Number(z.y),["z"]=Number(s*z.z),["px"]=Number(s*z.px),["py"]=Number(z.py),["pz"]=Number(z.pz),["bl"]=Number(z.bl),["br"]=Number(z.br),["at"]=Number(unixMilliseconds())};
                storage.Set("lm_calib:"+calibId,c.ToString(Formatting.None));
            }
            catch { /* Source persistence is best effort, including private mode. */ }
        }
        public void RestoreCalibration()
        {
            if(string.IsNullOrEmpty(calibId)||storage==null)return;
            try
            {
                string json=storage.Get("lm_calib:"+calibId);if(string.IsNullOrEmpty(json))return;
                StrictJson.Validate(json);var c=JToken.Parse(json) as JObject;if(c==null)return;
                bool Finite(string key){var v=c[key];if(v==null||(v.Type!=JTokenType.Integer&&v.Type!=JTokenType.Float))return false;double n=(double)v;return !double.IsNaN(n)&&!double.IsInfinity(n);}
                if(!new[]{"x","y","z","bl","br"}.All(Finite))return;
                double s=mirror?1:-1;posKnown=new[]{"px","py","pz"}.All(Finite);
                zero=new FacePose{x=s*(double)c["x"],y=(double)c["y"],z=s*(double)c["z"],bl=Params.Clamp((double)c["bl"],0,.5),br=Params.Clamp((double)c["br"],0,.5),
                    px=posKnown?s*(double)c["px"]:0,py=posKnown?(double)c["py"]:0,pz=posKnown?(double)c["pz"]:0};
                calibrated=true;
            }
            catch { /* Source ignores malformed JSON and unavailable storage. */ }
        }
        public void Apply(Params p, double dt)
        {
            var a = Math.Min(1, dt * 30); var fast = Math.Min(1, dt * 25);
            foreach (var pair in target)
            {
                var k = pair.Key; var v = pair.Value;
                bool closing = (k == "eyeLOpen" || k == "eyeROpen") && v < p[k];
                var rate = closing ? 1 : k.StartsWith("eye", StringComparison.Ordinal) || k == "mouthOpen" ? fast : k == "smile" ? Math.Min(1, dt * 8.4) : a;
                p[k] += (v - p[k]) * rate;
            }
        }
    }
}
