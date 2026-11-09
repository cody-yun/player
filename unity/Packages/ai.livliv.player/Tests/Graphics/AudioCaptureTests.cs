using System;
using LivLiv.Platform;
using LivLiv.Unity;
using NUnit.Framework;
using UnityEngine;
namespace LivLiv.Tests
{
    public sealed class AudioCaptureTests
    {
        [Test]public void StoppedOwnedMicrophoneReleasesOnceAndKeepsTerminalStatus()
        {
            var clip=AudioClip.Create("LivLiv stopped capture",2048,1,16000,false);bool recording=true;int releases=0;
            try
            {
                using var input=new AudioCaptureInput(clip,()=>0,()=>0,()=>releases++,()=>recording);
                Assert.That(input.Read(),Is.Zero);Assert.That(input.Error,Is.Null,"silence is not device failure");
                recording=false;Assert.That(input.Error,Does.Contain("Microphone capture ended"));
                Assert.That(input.Read(),Is.Zero);Assert.That(input.Level,Is.Zero);Assert.That(input.Vowel,Is.Zero);
                input.Dispose();input.Dispose();Assert.That(releases,Is.EqualTo(1));Assert.That(input.Error,Is.Not.Null);
                Assert.That(clip!=null,Is.True,"external owner retains its clip");
            }
            finally{TextureDecoder.Destroy(clip);}
        }
        [Test]public void ExplicitDisposeDoesNotReportDeviceFailureOrPollRecordingAgain()
        {
            var clip=AudioClip.Create("LivLiv disposed capture",2048,1,16000,false);int polls=0,releases=0;
            try
            {
                var input=new AudioCaptureInput(clip,()=>0,()=>0,()=>releases++,()=>{polls++;return true;});
                input.Dispose();Assert.That(input.Error,Is.Null);Assert.That(input.Read(),Is.Zero);
                Assert.That(polls,Is.Zero);Assert.That(releases,Is.EqualTo(1));
            }
            finally{TextureDecoder.Destroy(clip);}
        }
        [Test]
        public void LongCaptureGapReadsNewestCompleteBuffer()
        {
            const int rate=16000,size=2048;var clip=AudioClip.Create("LivLiv overrun fixture",size,1,rate,false);
            var ring=new float[size];for(int i=0;i<size;i++)ring[i]=(float)(.2*Math.Sin(i*.13));clip.SetData(ring,0);
            int position=513;double now=0;
            try
            {
                using(var input=new AudioCaptureInput(clip,()=>position,()=>now))using(var expected=new PcmVoiceInput(rate,()=>now))
                {
                    now=900;var newest=new float[size];for(int i=0;i<size;i++)newest[i]=ring[(position+i)%size];expected.Push(newest,0,size);
                    Assert.That(input.Read(),Is.EqualTo(expected.Read()).Within(1e-12));Assert.That(input.Vowel,Is.EqualTo(expected.Vowel).Within(1e-12));
                    position=-1;Assert.That(input.Read(),Is.Zero,"device stopped");
                }
            }
            finally{TextureDecoder.Destroy(clip);}
        }
        [TestCase(1),TestCase(2)]
        public void CircularClipCaptureMatchesContinuousPcmAndOwnsOnlyItsRelease(int channels)
        {
            const int rate=16000,size=4096;var clip=AudioClip.Create("LivLiv capture fixture",size,channels,rate,false);
            var ring=new float[size*channels];for(int i=0;i<size;i++)for(int c=0;c<channels;c++)ring[i*channels+c]=(float)(.15*Math.Sin(i*.17+c*.2));clip.SetData(ring,0);
            int position=0,releases=0,previous=0;double now=0;
            try
            {
                using(var actual=new AudioCaptureInput(clip,()=>position,()=>now,()=>releases++))using(var expected=new PcmVoiceInput(rate,()=>now))
                {
                    for(int step=0;step<30;step++)
                    {
                        int count=step%3==0?700:512;position=(previous+count)%size;now+=count*1000d/rate;
                        var incoming=new float[count*channels];for(int i=0;i<count;i++)for(int c=0;c<channels;c++)incoming[i*channels+c]=ring[((previous+i)%size)*channels+c];expected.Push(incoming,0,incoming.Length,channels);
                        Assert.That(actual.Read(),Is.EqualTo(expected.Read()).Within(1e-12),"step "+step);Assert.That(actual.Vowel,Is.EqualTo(expected.Vowel).Within(1e-12));previous=position;
                    }
                    actual.Dispose();actual.Dispose();Assert.That(actual.Read(),Is.Zero);Assert.That(releases,Is.EqualTo(1));Assert.That(clip!=null,Is.True,"external clip stays owned by caller");
                }
            }
            finally{TextureDecoder.Destroy(clip);}
        }
    }
}
