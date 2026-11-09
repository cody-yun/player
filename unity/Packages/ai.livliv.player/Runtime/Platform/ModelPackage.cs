// Unity/.NET file adapter for index.ts fromFiles and puppet.ts loadModel resource resolution.
// It retains source data; no GPU objects or image decoding happen here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using LivLiv.Runtime;

namespace LivLiv.Platform
{
    public sealed class ModelPackage
    {
        public readonly JObject model;
        public readonly JToken keyforms;
        public readonly IReadOnlyList<string> warnings;
        private readonly Func<string,int,byte[]> read;
        private ModelPackage(Func<string,int,byte[]> read,string lang)
        {
            this.read=read;
            try { model=ModelFormat.ReadModel(Parse(read("model.json",ModelFormat.Limits.ModelJson)),lang); }
            catch (JsonException e) { throw new LivLivError("not-a-model","LivLiv: model.json is not JSON",cause:e); }
            catch (LivLivError e) when (!e.Message.StartsWith("LivLiv",StringComparison.Ordinal))
            { throw new LivLivError(e.code,"LivLiv: "+e.Message,e.status,e); }
            var notes=new List<string>(); var path=(string)model["keyforms"];
            if (!string.IsNullOrEmpty(path))
            {
                try { keyforms=Parse(read(path,ModelFormat.Limits.Keyforms)); }
                catch (Exception e) when (e is JsonException || e is LivLivError || e is IOException)
                { notes.Add("LivLiv: "+path+" is missing or unreadable; the character plays on the rig alone"); }
            }
            warnings=notes.AsReadOnly();
        }
        // JSON dates stay strings, matching JSON.parse; bounded depth prevents hostile stack exhaustion.
        private static JToken Parse(byte[] bytes)
        {
            var json=Encoding.UTF8.GetString(bytes);
            if(json.Length>0&&json[0]=='\uFEFF')json=json.Substring(1); // Blob.text UTF-8 BOM behavior.
            StrictJson.Validate(json);
            using (var text=new StringReader(json))
            using (var reader=new JsonTextReader(text) { DateParseHandling=DateParseHandling.None, MaxDepth=128 })
            {
                var value=JToken.ReadFrom(reader);
                while (reader.Read()) if (reader.TokenType!=JsonToken.Comment) throw new JsonReaderException("Trailing JSON content");
                return value;
            }
        }
        public byte[] ReadImage(JObject layer,string path,bool png=false)
        {
            if (!ModelFormat.IsPackagePath(path)) throw new LivLivError("invalid","Invalid package image path: "+path);
            if ((bool?)layer["webp"]==true && !png && path.EndsWith(".png",StringComparison.Ordinal)) path=path.Substring(0,path.Length-4)+".webp";
            return read(path,LivLivZip.Bytes);
        }
        public static ModelPackage FromZip(byte[] bytes,string lang="en",CancellationToken cancellation=default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (bytes!=null && bytes.Length>=4 && bytes[0]==0x67 && bytes[1]==0x6c && bytes[2]==0x54 && bytes[3]==0x46) throw WrongBody();
            if (!LivLivZip.IsZip(bytes)) throw new LivLivError("not-a-model","LivLiv: this file is not a .livliv package (not a ZIP)");
            return FromFilesCore(LivLivZip.Unzip(bytes,cancellation),lang,false,cancellation);
        }
        public static ModelPackage FromFiles(IReadOnlyDictionary<string,byte[]> files,string lang="en")
            =>FromFiles(files,lang,default);
        public static ModelPackage FromFiles(IReadOnlyDictionary<string,byte[]> files,string lang,CancellationToken cancellation)
            =>FromFilesCore(files,lang,true,cancellation);
        private static LivLivError WrongBody()=>new LivLivError("not-a-model","LivLiv: this is a VRM or glTF file, and a Player plays LivLiv models: use a BodyPlayer with VrmBody (@livliv-dev/player/vrm)");
        private static ModelPackage FromFilesCore(IReadOnlyDictionary<string,byte[]> files,string lang,bool picked,CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var normalized=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            foreach (var f in files) normalized[Normalize(f.Key)]=f.Value;
            bool IsJson(string k)=>k=="model.json"||k.EndsWith("/model.json",StringComparison.Ordinal);
            // index.ts: a direct model wins. Otherwise the first ZIP replaces the entire picked list.
            // An unpacked blob is not searched recursively for another archive.
            if (picked && !normalized.Keys.Any(IsJson))
                foreach (var bytes in normalized.Values)
                    if (LivLivZip.IsZip(bytes)) { normalized=LivLivZip.Unzip(bytes,cancellation);break; }
            var json=normalized.Keys.Where(IsJson).OrderBy(k=>k.Length).FirstOrDefault();
            if (json==null && normalized.Keys.Any(k=>k.EndsWith(".vrm",StringComparison.OrdinalIgnoreCase)||k.EndsWith(".glb",StringComparison.OrdinalIgnoreCase)))throw WrongBody();
            if (json==null) throw new LivLivError("missing-file","LivLiv: no model.json among the files");
            var dir=json.Substring(0,json.Length-10);
            var byName=new Dictionary<string,byte[]>(StringComparer.Ordinal);
            foreach (var f in normalized) byName[f.Key.Substring(f.Key.LastIndexOf('/')+1)]=f.Value;
            return new ModelPackage((path,cap)=>
            {
                if (!ModelFormat.IsPackagePath(path)) throw new LivLivError("invalid","Invalid package path: "+path);
                if (!normalized.TryGetValue(dir+Normalize(path),out var b) && !byName.TryGetValue(path.Substring(path.LastIndexOf('/')+1),out b)) throw new LivLivError("missing-file","LivLiv: "+path+" is not among the files");
                if (b.Length>cap) throw new LivLivError("too-large","LivLiv: "+path+" is larger than "+cap+" bytes");
                return b;
            },lang);
        }
        private static string Normalize(string p)
        {
            p=p.Replace('\\','/');
            while (p.StartsWith("/",StringComparison.Ordinal)||p.StartsWith("./",StringComparison.Ordinal)) p=p.StartsWith("./",StringComparison.Ordinal)?p.Substring(2):p.Substring(1);
            return p;
        }
        public static ModelPackage FromDirectory(string directory,string lang="en")
        {
            var root=Path.GetFullPath(directory);
            return new ModelPackage((path,cap)=>
            {
                if (!ModelFormat.IsPackagePath(path)) throw new LivLivError("invalid","Invalid package path: "+path);
                // Reject links below the caller-selected root. Path syntax alone does not confine a local filesystem read.
                var full=root;
                foreach (var part in path.Split('/'))
                {
                    full=Path.Combine(full,part);
                    if (!File.Exists(full)&&!Directory.Exists(full)) throw new LivLivError("missing-file","LivLiv: "+path+" is not among the files");
                    if ((File.GetAttributes(full)&FileAttributes.ReparsePoint)!=0) throw new LivLivError("invalid","LivLiv: linked package file: "+path);
                }
                using (var f=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    if (f.Length>cap) throw new LivLivError("too-large","LivLiv: "+path+" is larger than "+cap+" bytes");
                    var b=new byte[(int)f.Length]; int at=0,n;
                    while (at<b.Length && (n=f.Read(b,at,b.Length-at))>0) at+=n;
                    if (at!=b.Length) throw new LivLivError("invalid","LivLiv: incomplete file: "+path);
                    return b;
                }
            },lang);
        }
    }
}
