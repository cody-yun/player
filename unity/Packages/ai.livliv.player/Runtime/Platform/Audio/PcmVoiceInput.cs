// WebAudio boundary for runtime/voice.ts: mono/stereo PCM -> 650 Hz Q .55 -> 1024 samples,
// plus unfiltered Blackman-window FFT power (smoothingTimeConstant=0). No Unity dependencies.
// W3C Web Audio 1.8.6 and BiquadFilterNode bandpass; browser fixtures exercise the actual nodes.
using System;
using LivLiv.Runtime;
namespace LivLiv.Platform
{
    public sealed class VoiceAnalysis
    {
        public const int Size=1024;
        public readonly int SampleRate;
        private readonly float[] raw=new float[Size],band=new float[Size];
        private readonly double[] window=new double[Size],re=new double[Size],im=new double[Size];
        private readonly double b0,b2,a1,a2;
        private double x1,x2,y1,y2;private int cursor;
        public VoiceAnalysis(int sampleRate)
        {
            if(sampleRate<8000||sampleRate>384000)throw new ArgumentOutOfRangeException(nameof(sampleRate));SampleRate=sampleRate;
            double w=2*Math.PI*650/sampleRate,alpha=Math.Sin(w)/(2*.55),a0=1+alpha;
            b0=alpha/a0;b2=-b0;a1=-2*Math.Cos(w)/a0;a2=(1-alpha)/a0;
            for(int i=0;i<Size;i++)window[i]=.42-.5*Math.Cos(2*Math.PI*i/Size)+.08*Math.Cos(4*Math.PI*i/Size);
        }
        public float Push(float sample)
        {
            double y=b0*sample+b2*x2-a1*y1-a2*y2;x2=x1;x1=sample;y2=y1;y1=y;
            float filtered=(float)y;raw[cursor]=sample;band[cursor]=filtered;cursor=(cursor+1)&(Size-1);return filtered;
        }
        public void CopyTime(float[] output,bool filtered=true)
        {
            if(output==null||output.Length!=Size)throw new ArgumentException("Expected 1024 samples",nameof(output));
            var source=filtered?band:raw;for(int i=0;i<Size;i++)output[i]=source[(cursor+i)&(Size-1)];
        }
        public void CopyPower(float[] output)
        {
            if(output==null||output.Length!=Size/2)throw new ArgumentException("Expected 512 bins",nameof(output));
            for(int i=0;i<Size;i++){re[i]=(float)(raw[(cursor+i)&(Size-1)]*window[i]);im[i]=0;}
            for(int i=1,j=0;i<Size;i++)
            {
                int bit=Size>>1;for(; (j&bit)!=0;bit>>=1)j^=bit;j^=bit;
                if(i<j){double v=re[i];re[i]=re[j];re[j]=v;}
            }
            for(int length=2;length<=Size;length<<=1)
            {
                double angle=-2*Math.PI/length,wr=Math.Cos(angle),wi=Math.Sin(angle);
                for(int start=0;start<Size;start+=length)
                {
                    double ur=1,ui=0;for(int j=0;j<length/2;j++)
                    {
                        int a=start+j,b=a+length/2;double vr=re[b]*ur-im[b]*ui,vi=re[b]*ui+im[b]*ur;
                        re[b]=re[a]-vr;im[b]=im[a]-vi;re[a]+=vr;im[a]+=vi;
                        double next=ur*wr-ui*wi;ui=ur*wi+ui*wr;ur=next;
                    }
                }
            }
            for(int i=0;i<Size/2;i++)
            {
                double power=(re[i]*re[i]+im[i]*im[i])/(Size*Size);
                // The original receives float dB, then stores Math.pow(10, dB / 10) in Float32Array.
                float db=(float)(10*Math.Log10(power));output[i]=(float)Math.Pow(10,db/10d);
            }
        }
    }
    public sealed class PcmVoiceInput : IVoiceInput
    {
        private readonly object gate=new object();private readonly VoiceAnalysis analysis;private readonly VoiceMeter meter=new VoiceMeter();
        private readonly float[] samples=new float[VoiceAnalysis.Size],power=new float[VoiceAnalysis.Size/2];
        private readonly Func<double> now;private bool disposed;
        public PcmVoiceInput(int sampleRate,Func<double> nowMilliseconds){analysis=new VoiceAnalysis(sampleRate);now=nowMilliseconds??throw new ArgumentNullException(nameof(nowMilliseconds));}
        public double Level {get{lock(gate)return meter.level;}}public double Vowel {get{lock(gate)return meter.vowel;}}
        public double Gain {get{lock(gate)return meter.gain;}set{lock(gate)meter.gain=value;}}
        // offset/count are float elements, interleaved. Mono or stereo, as in the source microphone/voice path.
        public void Push(float[] pcm,int offset,int count,int channels=1)
        {
            if(pcm==null)throw new ArgumentNullException(nameof(pcm));if(channels!=1&&channels!=2)throw new ArgumentOutOfRangeException(nameof(channels));
            if(offset<0||count<0||offset>pcm.Length-count||count%channels!=0)throw new ArgumentOutOfRangeException(nameof(count));
            lock(gate){if(disposed)return;for(int i=offset;i<offset+count;i+=channels)analysis.Push(channels==1?pcm[i]:(float)(((double)pcm[i]+pcm[i+1])*.5));}
        }
        public double Read()
        {
            lock(gate){if(disposed)return 0;double time=now();analysis.CopyTime(samples);analysis.CopyPower(power);return meter.ReadAt(samples,power,(double)analysis.SampleRate/VoiceAnalysis.Size,time);}
        }
        public void Dispose(){lock(gate)disposed=true;}
    }
}
