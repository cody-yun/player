// Port of src/runtime/format.ts. Copyright 2026 LivLiv. Apache-2.0.
// JObject retains unknown fields and the distinction between missing and JSON null.
// UI language is explicit (default en); no process-global locale or browser dependency.
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace LivLiv.Runtime
{
    public static class ModelFormat
    {
        public const string Format = "livliv-model", LegacyFormat = "livemaker-puppet";
        public const int FormatVersion = 4, LegacyVersion = 3;
        public static readonly IReadOnlyList<string> Features = Array.AsReadOnly(Array.Empty<string>());
        public static class Limits
        {
            public const int Canvas = 8192, Layers = 512, Images = 2048, Expressions = 256, Eyes = 16, Chains = 64, Points = 256;
            public const int Texture = 8192, Texels = 64 * (1 << 20), ModelJson = 4 * (1 << 20), Keyforms = 32 * (1 << 20);
            public const int Vertices = 1 << 21, Skinned = 1 << 20;
        }
        private const double Big = 1e6;
        private static readonly string[] Groups = { "head", "body", "hair_back", "hair_side", "hair_front", "hair_inner" };
        private static readonly Regex ImagePath = new Regex(@"^textures/[A-Za-z0-9_][A-Za-z0-9_.-]*\.(?:webp|png)$", RegexOptions.CultureInvariant);
        private static readonly Regex FilePath = new Regex(@"^(?!.*(?:^|/)\.\.(?:/|$))[A-Za-z0-9_][A-Za-z0-9_.-]*(?:/[A-Za-z0-9_][A-Za-z0-9_.-]*)*$", RegexOptions.CultureInvariant);
        private static readonly ConditionalWeakTable<JObject, object> Read = new ConditionalWeakTable<JObject, object>();
        public static int LatticeCell(double w, double h) => (int)Math.Max(6, Math.Floor(Math.Max(w, h) / 128 + .5));
        public static bool IsPackagePath(string path) => path != null && FilePath.IsMatch(path);
        private static JToken F(JToken o, string key) => (o as JObject)?[key];
        private static bool Nil(JToken v) => v == null || v.Type == JTokenType.Null;
        private static bool Num(JToken v) => v != null && (v.Type == JTokenType.Integer || v.Type == JTokenType.Float) && !double.IsNaN((double)v) && !double.IsInfinity((double)v);
        private static bool Text(JToken v) => v?.Type == JTokenType.String;
        private static string S(JToken v) => Text(v) ? (string)v : Nil(v) ? (v == null ? "undefined" : "null") : v.ToString(Newtonsoft.Json.Formatting.None);
        private static double N(JToken v) => Num(v) ? (double)v : double.NaN;
        private static double ToJsNumber(JToken v)
        {
            if (Num(v)) return (double)v;
            if (v?.Type == JTokenType.Boolean) return (bool)v ? 1 : 0;
            if (Text(v)) { var text = ((string)v).Trim(); return text.Length == 0 ? 0 : double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : double.NaN; }
            return double.NaN;
        }
        private static bool InRange(JToken v) => Num(v) && Math.Abs((double)v) <= Big;
        private static bool Truthy(JToken v) => !Nil(v) && (v.Type != JTokenType.Boolean || (bool)v) && (!Text(v) || ((string)v).Length > 0) && (!Num(v) || (double)v != 0);
        private static string Short(string s, int n) => s.Length > n ? s.Substring(0, n) : s;
        private static void Bad(string why, string code = "invalid") => throw new LivLivError(code, "this model is not valid: " + why);
        private static JArray List(JToken v, int most, string what)
        {
            if (v == null) return new JArray();
            if (!(v is JArray a) || a.Count > most) { Bad($"{what} is not a list of at most {most}"); return null; }
            return a;
        }
        private static void Sane(JToken v, string what) { if (!Nil(v) && !InRange(v)) Bad(what + " is not a number in range"); }
        private static void Nums(JToken v, int lo, int hi, string what)
        {
            if (Nil(v)) return;
            if (!(v is JArray a) || a.Count < lo || a.Count > hi || !a.All(InRange)) Bad($"{what} is not a list of {lo} to {hi} numbers");
        }
        private static void Fields(JToken o, string names, string what)
        {
            if (Nil(o)) return;
            if (!(o is JObject) && !(o is JArray)) Bad(what + " is not an object");
            foreach (var n in names.Split(' ')) Sane(F(o, n), what + "." + n);
        }
        private static void Weights(JToken o, string what)
        {
            if (Nil(o)) return;
            if (!(o is JObject obj)) { Bad(what + " is not an object"); return; }
            foreach (var p in obj.Properties()) Sane(p.Value, what + "." + Short(p.Name, 60));
        }
        private static void Copy(JObject to, JToken from, params string[] names)
        { foreach (var n in names) if (F(from, n) is JToken v) to[n] = v.DeepClone(); }
        // Object spread in lower() also copies enumerable array/string indices in old loosely-shaped files.
        private static JObject Spread(JToken source)
        {
            if (source is JObject obj) return (JObject)obj.DeepClone();
            var result = new JObject();
            if (source is JArray array) for (int i = 0; i < array.Count; i++) result[i.ToString(System.Globalization.CultureInfo.InvariantCulture)] = array[i].DeepClone();
            else if (Text(source)) { var s = (string)source; for (int i=0;i<s.Length;i++) result[i.ToString(System.Globalization.CultureInfo.InvariantCulture)] = s[i].ToString(); }
            return result;
        }

        public static JObject ReadModel(JToken raw, string lang = "en")
        {
            if (!(raw is JObject m)) throw new LivLivError("not-a-model", "not a LivLiv model (model.json is not an object)");
            if (Read.TryGetValue(m, out _)) return m;
            if (m["format"] == null || (Text(m["format"]) && (string)m["format"] == LegacyFormat))
            {
                var v = Nil(m["version"]) ? LegacyVersion : ToJsNumber(m["version"]);
                if (v > LegacyVersion) throw new LivLivError("not-a-model", $"this model says format \"{LegacyFormat}\" version {v}, which never existed");
                Check(m); Read.Add(m, new object()); return m;
            }
            if (!Text(m["format"]) || (string)m["format"] != Format) throw new LivLivError("not-a-model", $"not a LivLiv model (format \"{S(m["format"])}\")");
            if (!Num(m["version"]) || (double)m["version"] > FormatVersion) throw new LivLivError("newer-format", $"this model is format version {S(m["version"])}; this player reads up to {FormatVersion}: update the player");
            if (Truthy(m["requires"]))
            {
                var requires = List(m["requires"], int.MaxValue, "requires");
                if (requires.Count > 0) throw new LivLivError("missing-feature", $"this model needs {string.Join(", ", requires.Select(S))}, which this player does not have: update the player");
            }
            var output = Lower(m, lang); Check(output); Read.Add(output, new object()); return output;
        }

        private static void Check(JObject m)
        {
            var canvas = m["canvas"] as JArray;
            var w = canvas != null && canvas.Count > 0 ? N(canvas[0]) : double.NaN;
            var h = canvas != null && canvas.Count > 1 ? N(canvas[1]) : double.NaN;
            if (!(w > 0 && h > 0 && w <= Limits.Canvas && h <= Limits.Canvas)) Bad($"its canvas {w} x {h} is out of range", w > Limits.Canvas || h > Limits.Canvas ? "too-large" : "invalid");
            if (!(m["layers"] is JArray layers) || layers.Count == 0) { Bad("it has no layers"); return; }
            if (layers.Count > Limits.Layers) Bad($"it has {layers.Count} layers, more than {Limits.Layers}", "too-large");
            var a = m["anchors"]; var hd = F(a, "head");
            if (Nil(hd) || !new[] { F(hd,"cx"), F(hd,"cy"), F(hd,"rx"), F(hd,"ry"), F(a,"neck_y"), F(a,"top_y"), F(a,"bottom_y"), F(a,"hair_root_y") }.All(InRange)
                || !(N(F(hd,"rx")) > 1e-3 && N(F(hd,"ry")) > 1e-3) || !(F(a,"face") is JArray face) || face.Count != 4 || !face.All(InRange)) Bad("its anchors are missing or not numbers");
            Sane(F(a,"collar_y"), "anchors.collar_y");
            Fields(F(a,"jaw"), "top chin half open round wide", "anchors.jaw");
            Fields(F(a,"chest"), "cx cy rx ry", "anchors.chest");
            foreach (var b in List(F(a,"eyes"), Limits.Eyes, "anchors.eyes")) { if (Nil(b)) Bad("anchors.eyes has an empty entry"); Nums(b,4,4,"anchors.eyes"); }
            foreach (var b in List(F(a,"brows"), Limits.Eyes, "anchors.brows")) Nums(b,4,4,"anchors.brows");
            foreach (var line in List(F(a,"brow_lines"), Limits.Eyes, "anchors.brow_lines"))
            {
                if (Nil(line)) continue;
                int n = List(F(line,"x"), Limits.Points, "a brow line").Count;
                if (n == 0) Bad("a brow line has no points");
                Nums(F(line,"x"),n,n,"a brow line"); Nums(Nil(F(line,"bottom")) ? new JArray() : F(line,"bottom"),n,n,"a brow line");
                Nums(Nil(F(line,"lid")) ? new JArray() : F(line,"lid"),n,n,"a brow line"); Nums(F(line,"cap"),n,n,"a brow line");
            }
            Fields(m["tuning"], "turn nod hair blink expr mouth breath hairFront hairBack", "tuning");
            var r = m["rig"];
            Fields(r,"yaw pitch roll bulge bulge_shape hair_limit wind breath sway brow pitch_ease jaw","rig");
            Nums(F(r,"bulge_extent"),3,5,"rig.bulge_extent"); Nums(F(r,"drift"),2,2,"rig.drift"); Nums(F(r,"hair_drive"),3,3,"rig.hair_drive");
            Fields(F(r,"body"),"yaw roll breath bust","rig.body"); Fields(F(r,"neck"),"twist reach","rig.neck");
            foreach (var k in new[] { "front", "back", "side" }) Nums(F(F(r,"hair"),k),5,5,"rig.hair."+k);
            foreach (var c in List(F(r,"chains"), Limits.Chains,"rig.chains"))
            {
                if (!Text(F(c,"id"))) Bad("a chain has no id");
                var what = "chain " + Short(S(F(c,"id")),60); var points = List(F(c,"points"),Limits.Points,what+"'s points");
                if (points.Count < 2) Bad(what+" has fewer than two points");
                foreach (var q in points) { if (Nil(q)) Bad(what+" has an empty point"); Nums(q,2,2,what+"'s points"); }
                if (!Truthy(F(c,"k"))) Bad(what+" has no k");
                Nums(F(c,"k"),2,2,what+"'s k");
                foreach (var n in new[] { "damp", "drive", "hang", "wind" }) if (!Num(F(c,n))) Bad(what+" has no "+n);
                Fields(c,"depth damp drive hang wind freq limit sigma reach",what); Weights(F(c,"bias"),what+"'s bias");
            }
            var cell = LatticeCell(w,h);
            var bangs = F(layers.FirstOrDefault(l => S(F(l,"group")) == "hair_front"),"bones") as JArray;
            double vertices = 0, skinned = 0, readPixels = 0; int images = 0;
            foreach (var l in layers)
            {
                if (!(l is JObject) || !Text(F(l,"id"))) Bad("a layer has no id");
                var what = "layer " + Short(S(F(l,"id")),60);
                var x = N(F(l,"x")); var y = N(F(l,"y")); var lw = N(F(l,"w")); var lh = N(F(l,"h"));
                if (!new[] { F(l,"x"), F(l,"y"), F(l,"w"), F(l,"h") }.All(Num) || !(lw>0 && lh>0) || x < -w || y < -h || x+lw>2*w || y+lh>2*h) Bad(what+"'s box is out of range");
                if (!Text(F(l,"texture"))) Bad(what+" has no texture");
                foreach (var p in new[] { F(l,"texture") }.Concat(List(F(l,"frames"),Limits.Images,what+"'s frames")).Concat(new[] { F(l,"mask"), F(l,"talk"), F(l,"brow_tex") }))
                {
                    if (p == null) continue;
                    if (!Text(p) || !IsPackagePath((string)p)) Bad(what+": \""+Short(S(p),80)+"\" is not a path inside the package");
                    if (++images > Limits.Images) Bad($"it names more than {Limits.Images} images");
                }
                if (!Text(F(l,"group")) || !Groups.Contains((string)F(l,"group"))) Bad(what+"'s group is not one a player knows");
                foreach (var n in new[] { "role", "clip", "param" }) if (!Nil(F(l,n)) && !Text(F(l,n))) Bad(what+"'s "+n+" is not text");
                var vis = F(l,"visemes");
                if (!Nil(vis) && (!(vis is JArray va) || !va.All(Text))) Bad(what+"'s visemes are not text");
                foreach (var n in new[] { "depth", "fall", "fall_end" }) Sane(F(l,n),what+"'s "+n);
                foreach (var n in new[] { "pivot", "range", "rigid" }) Nums(F(l,n),2,2,what+"'s "+n);
                foreach (var n in new[] { "blush", "show", "hide" }) Weights(F(l,n),what+"'s "+n);
                var bones = List(F(l,"bones"),Limits.Chains,what+"'s bones");
                if (bones.Distinct(JToken.EqualityComparer).Count() != bones.Count) Bad(what+" names a chain twice");
                if (!bones.All(Text)) Bad(what+"'s bones are not chain ids");
                var verts = (Math.Ceiling((x+lw)/cell)-Math.Floor(x/cell)+1)*(Math.Ceiling((y+lh)/cell)-Math.Floor(y/cell)+1);
                var overBangs = S(F(l,"id")) == "brows" || S(F(l,"role")) == "expr_brows" || Truthy(F(l,"brow_tex"));
                vertices += verts; skinned += verts*(bones.Count+(overBangs ? bangs?.Count ?? 0 : 0));
                if (overBangs) readPixels += lw*lh;
            }
            if (vertices > Limits.Vertices) Bad($"its layers' meshes would have {vertices} vertices, more than {Limits.Vertices}","too-large");
            if (skinned > Limits.Skinned) Bad($"its chains would bend {skinned} vertices, more than {Limits.Skinned}","too-large");
            if (readPixels > Limits.Texels) Bad($"its brows layers cover {readPixels} pixels, more than {Limits.Texels}","too-large");
            foreach (var e in List(m["expressions"],Limits.Expressions,"expressions")) if (!Text(F(e,"id"))) Bad("an expression has no id");
            if (m["keyforms"] != null && (!Text(m["keyforms"]) || !IsPackagePath((string)m["keyforms"]))) Bad("keyforms is not a path inside the package");
            if (!Nil(m["watermark"]) && !Text(m["watermark"])) Bad("its watermark is not text");
        }

        private static JObject Lower(JObject m, string lang)
        {
            if (!(m["layers"] is JArray layers) || layers.Count == 0) throw new LivLivError("invalid","this model has no layers");
            if (layers.Count > Limits.Layers) throw new LivLivError("too-large",$"this model has {layers.Count} layers, more than {Limits.Layers}");
            var cv = m["canvas"] as JArray; var w = cv?.Count > 0 ? N(cv[0]) : double.NaN; var h = cv?.Count > 1 ? N(cv[1]) : double.NaN;
            if (!(w>0 && h>0 && w<=Limits.Canvas && h<=Limits.Canvas)) throw new LivLivError(w>Limits.Canvas || h>Limits.Canvas ? "too-large":"invalid",$"this model's canvas {w} x {h} is out of range");
            foreach (var l in layers)
            {
                if (!(l is JObject)) Bad("a layer is not an object");
                foreach (var p in new[] { F(l,"texture") }.Concat(Nil(F(l,"frames")) ? new JArray() : List(F(l,"frames"),int.MaxValue,"frames")).Concat(new[] { F(l,"mask"),F(l,"talk"),F(l,"brow_tex") }))
                    if (p != null && (!Text(p) || !ImagePath.IsMatch((string)p))) throw new LivLivError("invalid",$"layer {S(F(l,"id"))}: \"{S(p)}\" is not an image path of the package");
            }
            if (m["files"] is JObject files) foreach (var p in files.Properties())
                if (!p.Name.StartsWith("x-",StringComparison.Ordinal) && (!Text(p.Value) || !IsPackagePath((string)p.Value))) throw new LivLivError("invalid",$"files.{p.Name}: \"{S(p.Value)}\" is not a path in the package");
            var lowered = new JArray();
            foreach (var l in layers.Cast<JObject>())
            {
                var next = (JObject)l.DeepClone(); next.Remove("label"); next.Remove("expression");
                if (Nil(l["depth"])) next["depth"] = 0;
                next["name"] = Truthy(l["label"]) ? PickLabel(l["label"] as JObject,"en",S(l["id"])) : S(l["id"]);
                if (Truthy(l["expression"])) next["param"] = l["expression"].DeepClone();
                lowered.Add(next);
            }
            var output = new JObject(); Copy(output,m,"format","version","name","canvas","anchors","rig","meta","files"); output["layers"] = lowered;
            if (Truthy(m["tuning"]))
            {
                var tuning = Spread(m["tuning"]);
                {
                    var t = (JObject)tuning.DeepClone(); t.Remove("hair_front"); t.Remove("hair_back");
                    if (tuning["hair_front"] != null) t["hairFront"] = tuning["hair_front"].DeepClone();
                    if (tuning["hair_back"] != null) t["hairBack"] = tuning["hair_back"].DeepClone();
                    output["tuning"] = t;
                }
            }
            var pfile = m["profile"];
            if (Truthy(pfile))
            {
                var input = new JObject(); Copy(input,pfile,"facing","glasses");
                var p = new JObject { ["label"]="", ["class"]=new JObject { ["input"]=input } }; Copy(p,pfile,"id"); output["profile"]=p;
            }
            else if (pfile != null) output["profile"] = pfile.DeepClone();
            var expressions = new JArray();
            foreach (var e in !Truthy(m["expressions"]) ? new JArray() : List(m["expressions"],int.MaxValue,"expressions"))
            {
                if (!(e is JObject)) Bad("an expression is not an object");
                var expr = new JObject { ["label"]=PickLabel(F(e,"label") as JObject,lang,S(F(e,"id"))) }; Copy(expr,e,"id");
                if (F(e,"label") != null) expr["labels"]=F(e,"label").DeepClone();
                if (Truthy(F(e,"hotkey"))) expr["key"]=F(e,"hotkey").DeepClone(); expressions.Add(expr);
            }
            output["expressions"]=expressions;
            if (F(m["files"],"keyforms") != null) output["keyforms"]=F(m["files"],"keyforms").DeepClone();
            var mark = F(m["meta"],"watermark"); var kind = S(F(mark,"kind"));
            output["watermark"] = kind == "livliv" ? new JValue("LivLiv") : kind == "text" ? F(mark,"text")?.DeepClone() ?? JValue.CreateNull() : JValue.CreateNull();
            return output;
        }

        public static string PickLabel(JObject labels, string lang, string fallback)
        {
            if (labels == null) return fallback;
            var keys = labels.Properties().Select(p=>p.Name).ToArray();
            var tag = string.IsNullOrEmpty(lang) ? "en" : lang; var baseTag = tag.Split('-')[0].ToLowerInvariant();
            bool hant = baseTag=="zh" && Regex.IsMatch(tag,@"-(hant|tw|hk|mo)\b",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            var tags = new List<string> { tag };
            if (baseTag=="zh") tags.Add(hant ? "zh-Hant":"zh-Hans"); tags.Add(baseTag);
            if (hant) tags.Add("zh"); tags.Add("en"); tags.Add("zh");
            foreach (var t in tags)
            {
                var k = keys.FirstOrDefault(key=>string.Equals(key,t,StringComparison.OrdinalIgnoreCase));
                if (k != null && Truthy(labels[k])) return S(labels[k]);
            }
            return keys.Length>0 ? S(labels[keys[0]]) : fallback;
        }
        public static string LangOf(string text)
        {
            if (Regex.IsMatch(text,@"[぀-ヿ]")) return "ja";
            if (Regex.IsMatch(text,@"[가-힯]")) return "ko";
            if (Regex.IsMatch(text,@"[一-鿿]")) return "zh";
            return "en";
        }
    }
}
