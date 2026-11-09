// Port of src/voice.ts mouth driver and live lip-sync controls. Copyright 2026 LivLiv. Apache-2.0.
// Audio devices/decoded speech queues are supplied by platform adapters.
using System;
using LivLiv.Runtime;
namespace LivLiv
{
    public interface IVoiceInput : IDisposable
    {double Read();double Vowel {get;}double Level {get;}double Gain {get;set;}}
    public sealed class LipSyncOptions { public double motion=1;public bool bounce; }
    // createSpeech counterpart. The platform factory supplies the queue's output/loader and
    // pumps SpeechPlayer.Tick independently of rendering. Stop retains the mouth driver.
    public sealed class Speech : IDisposable
    {
        private readonly SpeechPlayer runtime;
        private Action remove;
        public Action onIdle;
        private Speech(SpeechPlayer runtime) { this.runtime=runtime; }
        public bool Busy=>runtime.Busy;
        public void Say(string url)=>runtime.Enqueue(url);
        public void Say(byte[] encodedAudio)=>runtime.Enqueue(encodedAudio);
        public void Stop()=>runtime.Stop();
        public void Unlock()=>runtime.Unlock();
        public void Dispose(){var end=remove;remove=null;end?.Invoke();}
        public static Speech Attach(IPlayerHost player,SpeechPlayer runtime,LipSyncOptions options=null,Func<double> random=null)
        {
            if(player==null)throw new ArgumentNullException(nameof(player));
            if(runtime==null)throw new ArgumentNullException(nameof(runtime));
            var speech=new Speech(runtime);
            runtime.onIdle=()=>speech.onIdle?.Invoke();
            try { speech.remove=player.Use(new MouthDriver(runtime.Read,runtime.Vowel,options,runtime.Dispose,random)); }
            catch { runtime.Dispose();throw; }
            return speech;
        }
    }
    public sealed class LipSync : IDisposable
    {
        private readonly IVoiceInput input;private Action remove;
        private LipSync(IVoiceInput input,Action remove){this.input=input;this.remove=remove;}
        public double Level=>input.Level;
        public double Gain {get=>input.Gain;set=>input.Gain=value;}
        public void Stop(){var end=remove;remove=null;end?.Invoke();}
        public void Dispose()=>Stop();
        public static LipSync Attach(IPlayerHost player,IVoiceInput input,LipSyncOptions options=null,Func<double> random=null)
        {
            if(player==null)throw new ArgumentNullException(nameof(player));if(input==null)throw new ArgumentNullException(nameof(input));
            return new LipSync(input,player.Use(new MouthDriver(input.Read,()=>input.Vowel,options,input.Dispose,random)));
        }
    }
    public sealed class MouthDriver : PlayerDriver
    {
        private readonly Func<double> read,vowel;private readonly Action release;
        private readonly LipSyncOptions options;private readonly TalkMotion talk;private bool disposed;
        public override int Order=>10;
        public MouthDriver(Func<double> read,Func<double> vowel,LipSyncOptions options=null,Action release=null,Func<double> random=null)
        {this.read=read??throw new ArgumentNullException(nameof(read));this.vowel=vowel??throw new ArgumentNullException(nameof(vowel));this.options=options??new LipSyncOptions();this.release=release;talk=new TalkMotion(random);}
        public override void Update(PlayerFrame frame,IPlayerHost player)
        {
            if(disposed)return;double level=read();var p=frame.@params;p.mouthOpen=Math.Max(p.mouthOpen,level);p.vowel=vowel();double amount=options.motion;
            if(!frame.tracked&&amount>0){talk.amount=amount;talk.bounce=options.bounce;frame.Add(talk.Update(level,frame.dt,p));if(talk.pauseBlink)player.Blink();frame.move.y+=talk.hop;}
            else if(talk.act!=0||talk.hop!=0)talk.Reset();
        }
        public override void Dispose(){if(disposed)return;disposed=true;release?.Invoke();}
    }
}
