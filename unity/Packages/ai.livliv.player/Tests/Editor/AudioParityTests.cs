using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using LivLiv.Platform;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
namespace LivLiv.Tests
{
    public sealed class AudioParityTests
    {
        private static JObject Load(){using(var f=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/audio.json.gz")))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var t=new StreamReader(z))using(var j=new JsonTextReader(t))return JObject.Load(j);}
        private static readonly Lazy<JObject> Data=new Lazy<JObject>(Load);
        public static IEnumerable<TestCaseData> Cases()=>Data.Value["cases"].Select(r=>new TestCaseData(r).SetName("WebAudio PCM "+r["rate"]));
        [TestCaseSource(nameof(Cases))]public void PcmFilterWindowAndSpectrumMatchBrowser(JToken row)
        {
            var a=new VoiceAnalysis((int)row["rate"]);var input=row["input"].ToObject<float[]>();var filtered=row["filtered"].ToObject<float[]>();
            for(int i=0;i<input.Length;i++)Assert.That(a.Push(input[i]),Is.EqualTo(filtered[i]).Within(2e-7),"filtered "+i);
            var time=new float[1024];a.CopyTime(time);var expected=row["time"].ToObject<float[]>();
            for(int i=0;i<time.Length;i++)Assert.That(time[i],Is.EqualTo(expected[i]).Within(2e-7),"time "+i);
            a.CopyTime(time,false);CollectionAssert.AreEqual(row["raw"].ToObject<float[]>(),time,"raw window");
            var power=new float[512];a.CopyPower(power);var spectrum=row["power"].ToObject<float[]>();
            for(int i=0;i<power.Length;i++)Assert.That(power[i],Is.EqualTo(spectrum[i]).Within(1e-11+Math.Abs(spectrum[i])*2e-5),"power "+i);
        }
        [Test]public void StereoChunkingMatchesMonoAndDisposalStopsReads()
        {
            var pcm=Data.Value["cases"][0]["input"].ToObject<float[]>();double now=100;
            using(var mono=new PcmVoiceInput(16000,()=>now))using(var stereo=new PcmVoiceInput(16000,()=>now))
            {
                var interleaved=pcm.SelectMany(v=>new[]{v,v}).ToArray();mono.Push(pcm,0,pcm.Length);
                for(int i=0;i<interleaved.Length;i+=256)stereo.Push(interleaved,i,256,2);
                Assert.That(stereo.Read(),Is.EqualTo(mono.Read()));Assert.That(stereo.Vowel,Is.EqualTo(mono.Vowel));
                stereo.Dispose();stereo.Push(interleaved,0,256,2);now+=20;Assert.That(stereo.Read(),Is.Zero);
            }
        }
        [Test]public void VoiceClockMatchesOriginalFirstZeroAndLongGapReads()
        {
            JObject data;using(var f=File.OpenRead(Path.Combine(ParityTests.RepoRoot,"unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/voice-clock.json.gz")))using(var z=new GZipStream(f,CompressionMode.Decompress))using(var t=new StreamReader(z))using(var j=new JsonTextReader(t))data=JObject.Load(j);
            using(var sha=SHA256.Create())foreach(var e in ((JObject)data["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,e.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)e.Value),e.Name);
            foreach(var c in data["cases"])
            {
                var meter=new LivLiv.Runtime.VoiceMeter();var samples=c["samples"].ToObject<float[]>();var power=c["power"].ToObject<float[]>();
                foreach(var row in c["rows"]){Assert.That(meter.ReadAt(samples,power,(double)c["rate"]/1024,(double)row["now"]),Is.EqualTo((double)row["level"]).Within(1e-9));Assert.That(meter.vowel,Is.EqualTo((double)row["vowel"]).Within(1e-9));}
            }
        }
        [Test]public void FixtureUsesCurrentSources(){using(var sha=SHA256.Create())foreach(var e in ((JObject)Data.Value["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(ParityTests.RepoRoot,e.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)e.Value),e.Name);}
    }
}
