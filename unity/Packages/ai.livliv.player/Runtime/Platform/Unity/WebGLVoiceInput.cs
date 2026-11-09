using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Runtime;
namespace LivLiv.Unity
{
    // JS owns the microphone/WebAudio graph; C# owns the source-compatible meter and lip-sync driver.
    public sealed class WebGLVoiceInput : IVoiceInput,LivLiv.Platform.ICaptureStatus
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]private static extern int LivLivVoice_Start();
        [DllImport("__Internal")]private static extern int LivLivVoice_State(int id);
        [DllImport("__Internal")]private static extern double LivLivVoice_Now();
        [DllImport("__Internal")]private static extern void LivLivVoice_Error(int id,[Out]byte[] error,int capacity);
        [DllImport("__Internal")]private static extern int LivLivVoice_Read(int id,[Out]float[] samples,[Out]float[] power);
        [DllImport("__Internal")]private static extern void LivLivVoice_Stop(int id);
#endif
        private readonly VoiceMeter meter=new VoiceMeter();private readonly float[] samples=new float[1024],power=new float[512];
        private int id;private bool disposed;
        private WebGLVoiceInput(int id){this.id=id;}
        public double Level=>meter.level;public double Vowel=>meter.vowel;public double Gain {get=>meter.gain;set=>meter.gain=value;}
        public bool NeedsGesture
        {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                return !disposed&&LivLivVoice_State(id)==2;
#else
                return false;
#endif
            }
        }
        public string Error
        {
            get {
#if UNITY_WEBGL && !UNITY_EDITOR
                if(disposed||LivLivVoice_State(id)>=0)return null;var buffer=new byte[2048];LivLivVoice_Error(id,buffer,buffer.Length);int end=Array.IndexOf(buffer,(byte)0);return Encoding.UTF8.GetString(buffer,0,end<0?buffer.Length:end);
#else
                return null;
#endif
            }
        }
        public static async Task<WebGLVoiceInput> StartAsync(CancellationToken cancellationToken=default)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            cancellationToken.ThrowIfCancellationRequested();var input=new WebGLVoiceInput(LivLivVoice_Start());
            // Cancel the JS request immediately, even if the hidden page stops
            // pumping Unity Task.Yield continuations. Late permission streams close in JS.
            using var cancellation=cancellationToken.Register(input.Dispose);
            try
            {
                while(LivLivVoice_State(input.id)==0){cancellationToken.ThrowIfCancellationRequested();await Task.Yield();}
                cancellationToken.ThrowIfCancellationRequested();if(LivLivVoice_State(input.id)<0)
                {
                    string error=input.Error;
                    if(error.StartsWith("NotAllowedError:",StringComparison.Ordinal)||error.StartsWith("SecurityError:",StringComparison.Ordinal))throw new UnauthorizedAccessException("LivLiv: "+error);
                    throw new LivLivError("missing-feature","LivLiv: "+error);
                }
                return input;
            }
            catch{input.Dispose();throw;}
#else
            await Task.CompletedTask;throw new LivLivError("missing-feature","LivLiv: browser audio is available only in WebGL players");
#endif
        }
        public double Read()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            if(disposed)return 0;double now=LivLivVoice_Now();int rate=LivLivVoice_Read(id,samples,power);if(rate==0)return 0;
            return meter.ReadAt(samples,power,rate/1024d,now);
#else
            return 0;
#endif
        }
        public void Dispose(){if(disposed)return;disposed=true;
#if UNITY_WEBGL && !UNITY_EDITOR
            LivLivVoice_Stop(id);
#endif
        }
    }
}
