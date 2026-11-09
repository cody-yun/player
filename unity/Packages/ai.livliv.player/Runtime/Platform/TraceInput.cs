using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
namespace LivLiv.Platform
{
    // Source camera-vs-trace boundary, used to suppress persisted calibration during replay.
    public interface IFaceInputMode { bool IsReplay {get;} }
    public sealed class TraceFaceInput:IFaceInput,IFaceInputMode
    {
        public readonly TraceSource Source;
        private bool disposed;private double countAt;private int counted;
        public TraceFaceInput(IReadOnlyList<FaceFrame> frames,bool synth=false){Source=new TraceSource(frames??throw new ArgumentNullException(nameof(frames)),synth);}
        public static TraceFaceInput Synthetic(bool narrow=false)=>new TraceFaceInput(Trace.SynthTrace(narrow),true);
        public static TraceFaceInput FromJson(string json)=>new TraceFaceInput(TraceData.Parse(json));
        public static async Task<TraceFaceInput> LoadAsync(Uri url,IModelTransport transport,CancellationToken cancellation=default)
        {
            if(url==null||!url.IsAbsoluteUri)throw new ArgumentException("Use an absolute trace URL",nameof(url));
            if(transport==null)throw new ArgumentNullException(nameof(transport));cancellation.ThrowIfCancellationRequested();
            var response=await transport.Get(url,16*1024*1024,false,cancellation);cancellation.ThrowIfCancellationRequested();
            if(response?.bytes==null)throw new LivLivError("invalid","Trace transport returned no bytes");
            if(response.bytes.Length>16*1024*1024)throw new LivLivError("too-large","Trace download exceeds 16 MiB");
            string json;try{json=new UTF8Encoding(false,true).GetString(response.bytes);}catch(DecoderFallbackException e){throw new LivLivError("invalid","Invalid face trace UTF-8",cause:e);}
            if(json.Length>0&&json[0]=='\ufeff')json=json.Substring(1);
            return FromJson(json);
        }
        public bool Ready=>!disposed;
        public bool IsReplay=>true;
        public bool Dark=>false;
        public double Fps {get;private set;}
        public FaceFrame Next(double nowMilliseconds)
        {
            if(disposed)return null;var frame=Source.Next(nowMilliseconds);if(frame!=null&&(frame.m!=null||frame.b!=null))counted++;
            if(nowMilliseconds-countAt>=1000){Fps=countAt!=0?Math.Floor(counted*1000/(nowMilliseconds-countAt)+.5):0;counted=0;countAt=nowMilliseconds;}
            return frame;
        }
        public void Dispose(){disposed=true;}
    }
    public sealed class RecordingFaceInput:IFaceInput,IFaceInputMode
    {
        private readonly IFaceInput input;private bool disposed;
        public readonly TraceRecorder Recorder;
        // Takes ownership of input. Recorder remains available after Stop/Dispose for export.
        public RecordingFaceInput(IFaceInput input,TraceRecorder recorder=null){this.input=input??throw new ArgumentNullException(nameof(input));Recorder=recorder??new TraceRecorder();}
        public bool Ready=>!disposed&&input.Ready;
        public bool IsReplay=>input is IFaceInputMode mode&&mode.IsReplay;
        public double Fps=>input.Fps;
        public bool Dark=>input.Dark;
        public FaceFrame Next(double nowMilliseconds){if(disposed)return null;var f=input.Next(nowMilliseconds);if(f!=null)Recorder.Push(IsReplay?new FaceFrame{t=nowMilliseconds,m=f.m,b=f.b,l=f.l}:f);return f;}
        public void Dispose(){if(disposed)return;disposed=true;input.Dispose();}
    }
    public static class TraceData
    {
        // Boundary validation for externally loaded recordings; core TraceSource remains source-faithful.
        public static FaceFrame[] Parse(string json)
        {
            if(json==null)throw new ArgumentNullException(nameof(json));
            if(json.Length>16*1024*1024)throw new LivLivError("too-large","Face trace JSON exceeds 16M characters");
            try
            {
                StrictJson.Validate(json);var rows=JToken.Parse(json) as JArray;if(rows==null)throw new Exception("Expected frame array");
                if(rows.Count>9000)throw new LivLivError("too-large","Face trace exceeds recorder's 9000-frame budget");
                var result=new FaceFrame[rows.Count];double previous=0;
                double Number(JToken v){if(v==null||(v.Type!=JTokenType.Integer&&v.Type!=JTokenType.Float))throw new Exception("Expected number");double n=(double)v;if(double.IsNaN(n)||double.IsInfinity(n))throw new Exception("Non-finite value");return n;}
                for(int i=0;i<rows.Count;i++)
                {
                    if(!(rows[i] is JObject row))throw new Exception("Expected frame object");double t=Number(row["t"]);if(t<previous||t+33==t)throw new Exception("Invalid frame clock");previous=t;
                    double[] Values(string name,int size){var token=row[name];if(token==null)return null;if(!(token is JArray a)||a.Count!=size)throw new Exception("Invalid "+name+" dimensions");var values=new double[size];for(int n=0;n<size;n++)values[n]=Number(a[n]);return values;}
                    result[i]=new FaceFrame{t=t,m=Values("m",16),b=Values("b",18),l=Values("l",8)};
                }
                return result;
            }
            catch(LivLivError){throw;}
            catch(Exception e){throw new LivLivError("invalid","Invalid face trace",cause:e);}
        }
    }
}
