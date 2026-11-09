// src/index.ts fetchPackage/loadNow and src/runtime/puppet.ts load/locate.
// Invoke on the Unity main thread (transport continuations retain its synchronization context).
// Transport and concurrency are Unity adapters; model paths/versions/progress retain source boundaries.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace LivLiv.Platform
{
    public sealed class ModelResponse
    {
        public byte[] bytes;
        public string lastModified;
    }
    public interface IModelTransport
    {
        Task<ModelResponse> Get(Uri url,int maxBytes,bool noCache,CancellationToken cancellation,Action<double> progress=null);
    }
    public static class ModelDownload
    {
        public static async Task<ModelPackage> Load(Uri url,LoadJob job,IModelTransport transport,string lang="en")
        {
            if(url==null||!url.IsAbsoluteUri||(url.Scheme!="http"&&url.Scheme!="https"))
                throw new ArgumentException("LivLiv: use an absolute HTTP(S) model URL",nameof(url));
            job.Cancellation.ThrowIfCancellationRequested();job.Progress(0);
            var path=url.AbsolutePath;
            if(path.EndsWith(".livliv",StringComparison.OrdinalIgnoreCase)||path.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))
            {
                ModelResponse response=null;
                for(int attempt=0;;attempt++)
                {
                    try{response=await transport.Get(url,LivLivZip.Bytes,false,job.Cancellation,job.Progress);break;}
                    catch(LivLivError e)
                    {
                        job.Cancellation.ThrowIfCancellationRequested();
                        if(attempt!=0||(e.code!="unreachable"&&(e.code!="http"||e.status<500)))throw;
                    }
                }
                job.Cancellation.ThrowIfCancellationRequested();job.Build();
                job.Cancellation.ThrowIfCancellationRequested();
                return ModelPackage.FromZip(response.bytes,lang,job.Cancellation);
            }
            var file=new UriBuilder(url){Fragment=""};
            if(!file.Path.EndsWith(".json",StringComparison.Ordinal))file.Path=file.Path.TrimEnd('/')+"/model.json";
            var json=await transport.Get(file.Uri,ModelFormat.Limits.ModelJson,true,job.Cancellation);
            var files=new Dictionary<string,byte[]>(StringComparer.Ordinal){["model.json"]=json.bytes};
            var model=ModelPackage.FromFiles(files,lang).model;
            var tag=(string)model["tex"];
            if(string.IsNullOrEmpty(tag))tag=VersionTag(model["layers"],json.lastModified);
            var baseUrl=new Uri(file.Uri,".");
            Uri Resource(string name)=>new Uri(baseUrl,name+"?v="+Uri.EscapeDataString(tag)+(url.Query.Length>1?"&"+url.Query.Substring(1):""));
            var paths=new List<string>();
            foreach(JObject layer in model["layers"])
            {
                void Add(string p){if(p!=null)paths.Add((bool?)layer["webp"]==true&&p.EndsWith(".png",StringComparison.Ordinal)?p.Substring(0,p.Length-4)+".webp":p);}
                Add((string)layer["texture"]);foreach(var f in layer["frames"]??new JArray())Add((string)f);
                Add((string)layer["mask"]);Add((string)layer["talk"]);Add((string)layer["brow_tex"]);
            }
            // Bounded simultaneous transfers avoid opening thousands of requests on a phone.
            using(var pending=CancellationTokenSource.CreateLinkedTokenSource(job.Cancellation))
            {
                long total=json.bytes.Length;int arrived=0;Exception failure=null;
                async Task Image(string name)
                {
                    pending.Token.ThrowIfCancellationRequested();
                    try
                    {
                        ModelResponse r;
                        try{r=await transport.Get(Resource(name),LivLivZip.Bytes,false,pending.Token);}
                        catch(LivLivError e) when(e.code=="http"||e.code=="unreachable")
                        {throw new LivLivError("missing-file","LivLiv: could not load the image "+name,cause:e);}
                        pending.Token.ThrowIfCancellationRequested();
                        if((total+=r.bytes.Length)>LivLivZip.Bytes)throw new LivLivError("too-large","LivLiv: downloaded model exceeds the byte budget");
                        files[name]=r.bytes;
                        job.Progress((double)++arrived/paths.Count);if(arrived==paths.Count)job.Build();
                    }
                    catch(Exception e){if(failure==null)failure=e;pending.Cancel();throw;}
                }
                async Task Keyforms()
                {
                    var name=(string)model["keyforms"];if(name==null)return;
                    try
                    {
                        var r=await transport.Get(Resource(name),ModelFormat.Limits.Keyforms,false,pending.Token);
                        pending.Token.ThrowIfCancellationRequested();
                        if((total+=r.bytes.Length)>LivLivZip.Bytes)throw new LivLivError("too-large","LivLiv: downloaded model exceeds the byte budget");
                        files[name]=r.bytes;
                    }
                    catch(LivLivError){/* Optional, like puppet.ts: malformed/missing keyforms keep the rig. */}
                }
                // SemaphoreSlim.WaitAsync schedules released waiters on the thread pool, which
                // has no workers in single-threaded WebGL. These six async lanes retain Unity's context.
                int next=0;
                async Task Lane(){while(next<paths.Count){pending.Token.ThrowIfCancellationRequested();int at=next++;await Image(paths[at]);}}
                var keyforms=Keyforms();
                try{await Task.WhenAll(Enumerable.Range(0,Math.Min(6,paths.Count)).Select(_=>Lane()).Concat(new[]{keyforms}));}
                catch{job.Cancellation.ThrowIfCancellationRequested();if(failure!=null)throw failure;throw;}
            }
            job.Cancellation.ThrowIfCancellationRequested();
            return ModelPackage.FromFiles(files,lang);
        }
        // JSON.stringify numbers differ from Json.NET (1 versus 1.0, exponent thresholds,
        // and Mono's round-trip precision). Keep this conversion local to the source cache key.
        private static string JsonForHash(JToken value)
        {
            if(value is JArray array)return "["+string.Join(",",array.Select(JsonForHash))+"]";
            if(value is JObject obj)return "{"+string.Join(",",obj.Properties().OrderBy(p=>uint.TryParse(p.Name,out uint n)&&n<uint.MaxValue&&n.ToString(CultureInfo.InvariantCulture)==p.Name?0:1).ThenBy(p=>uint.TryParse(p.Name,out uint n)&&n<uint.MaxValue&&n.ToString(CultureInfo.InvariantCulture)==p.Name?n:0).Select(p=>JsonForHash(new JValue(p.Name))+":"+JsonForHash(p.Value)))+"}";
            if(value.Type==JTokenType.Integer||value.Type==JTokenType.Float)
            {
                double number=(double)value;if(number==0)return "0";
                string digits=null;
                for(int precision=1;precision<=17;precision++)
                {digits=number.ToString("G"+precision,CultureInfo.InvariantCulture);if(double.Parse(digits,CultureInfo.InvariantCulture)==number)break;}
                int at=digits.IndexOf('E');if(at<0)return digits;
                int exponent=int.Parse(digits.Substring(at+1),CultureInfo.InvariantCulture);string mantissa=digits.Substring(0,at);
                if(exponent>=-6&&exponent<21)
                {
                    string sign=mantissa.StartsWith("-",StringComparison.Ordinal)?"-":"";
                    mantissa=mantissa.TrimStart('-').Replace(".","");int point=exponent+1;
                    return sign+(point<=0?"0."+new string('0',-point)+mantissa:point>=mantissa.Length?mantissa+new string('0',point-mantissa.Length):mantissa.Insert(point,"."));
                }
                return mantissa+"e"+(exponent>=0?"+":"")+exponent.ToString(CultureInfo.InvariantCulture);
            }
            if(value.Type==JTokenType.String)
            {
                var b=new StringBuilder("\"");
                foreach(char c in (string)value)
                {
                    switch(c)
                    {
                        case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;
                        case '\b':b.Append("\\b");break;case '\f':b.Append("\\f");break;
                        case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;
                        default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;
                    }
                }
                return b.Append('"').ToString();
            }
            return value.ToString(Formatting.None);
        }
        public static string VersionTag(JToken layers,string lastModified)
        {
            // tex is preferred. Fallback is the source 31x UTF-16 hash/base36 over canonical JSON plus Last-Modified.
            var text=JsonForHash(layers)+(lastModified??"");uint hash=0;
            unchecked{foreach(char c in text)hash=hash*31+c;}
            const string digits="0123456789abcdefghijklmnopqrstuvwxyz";var result="";
            do{result=digits[(int)(hash%36)]+result;hash/=36;}while(hash!=0);
            return result;
        }
    }
}
