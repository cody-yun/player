// Port of src/runtime/params.ts. Copyright 2026 LivLiv. Apache-2.0.
// Unity adaptation: mutable reference type with a named indexer; source parameter names retained.
using System;
using System.Collections.Generic;
namespace LivLiv.Runtime
{
    [Serializable]
    public sealed class Params
    {
        public double angleX;
        public double angleY;
        public double angleZ;
        public double eyeLOpen = 1;
        public double eyeROpen = 1;
        public double eyeBallX;
        public double eyeBallY;
        public double browLY;
        public double browRY;
        public double mouthOpen;
        public double smile;
        public double vowel;
        public double bodyX;
        public double bodyZ;
        public double breath;
        public static readonly IReadOnlyDictionary<string, double[]> Ranges = new Dictionary<string, double[]>
        {
            ["angleX"] = new[] { -30d, 30d },
            ["angleY"] = new[] { -30d, 30d },
            ["angleZ"] = new[] { -30d, 30d },
            ["eyeLOpen"] = new[] { 0d, 1d },
            ["eyeROpen"] = new[] { 0d, 1d },
            ["eyeBallX"] = new[] { -1d, 1d },
            ["eyeBallY"] = new[] { -1d, 1d },
            ["browLY"] = new[] { -1d, 1d },
            ["browRY"] = new[] { -1d, 1d },
            ["mouthOpen"] = new[] { 0d, 1d },
            ["smile"] = new[] { 0d, 1d },
            ["vowel"] = new[] { -1d, 1d },
            ["bodyX"] = new[] { -10d, 10d },
            ["bodyZ"] = new[] { -10d, 10d },
            ["breath"] = new[] { 0d, 1d }
        };
        public double this[string key]
        {
            get
            {
                switch (key)
                {
                    case "angleX": return angleX;
                    case "angleY": return angleY;
                    case "angleZ": return angleZ;
                    case "eyeLOpen": return eyeLOpen;
                    case "eyeROpen": return eyeROpen;
                    case "eyeBallX": return eyeBallX;
                    case "eyeBallY": return eyeBallY;
                    case "browLY": return browLY;
                    case "browRY": return browRY;
                    case "mouthOpen": return mouthOpen;
                    case "smile": return smile;
                    case "vowel": return vowel;
                    case "bodyX": return bodyX;
                    case "bodyZ": return bodyZ;
                    case "breath": return breath;
                    default: throw new ArgumentException("Unknown parameter: " + key);
                }
            }
            set
            {
                switch (key)
                {
                    case "angleX": angleX = value; break;
                    case "angleY": angleY = value; break;
                    case "angleZ": angleZ = value; break;
                    case "eyeLOpen": eyeLOpen = value; break;
                    case "eyeROpen": eyeROpen = value; break;
                    case "eyeBallX": eyeBallX = value; break;
                    case "eyeBallY": eyeBallY = value; break;
                    case "browLY": browLY = value; break;
                    case "browRY": browRY = value; break;
                    case "mouthOpen": mouthOpen = value; break;
                    case "smile": smile = value; break;
                    case "vowel": vowel = value; break;
                    case "bodyX": bodyX = value; break;
                    case "bodyZ": bodyZ = value; break;
                    case "breath": breath = value; break;
                    default: throw new ArgumentException("Unknown parameter: " + key);
                }
            }
        }
        public Params Clone() => (Params)MemberwiseClone();
        public static double Clamp(double x, double a, double b) => Math.Min(b, Math.Max(a, x));
        public static double Smooth(double a, double b, double x)
        {
            var t = Clamp((x - a) / (b - a), 0, 1);
            return t * t * (3 - 2 * t);
        }
    }
}
