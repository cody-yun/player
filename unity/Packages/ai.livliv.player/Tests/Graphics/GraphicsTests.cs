using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LivLiv.Platform;
using LivLiv.Unity;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivLiv.Tests
{
    [Category("Graphics")]
    public sealed partial class GraphicsTests
    {
        private static string PackageRoot=>Path.GetFullPath(Path.Combine(Application.dataPath,"../Packages/ai.livliv.player"));
        [SetUp] public void RequireGraphics()
        { Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null),"Run graphics suite WITHOUT -nographics"); }
        [TestCase("png"),TestCase("webp")]
        public void DecodeOrientationAndPremultiplication(string ext)
        {
            var decoder=new TextureDecoder();var texture=decoder.Decode(File.ReadAllBytes(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/colors."+ext)));
            try
            {
                Assert.That(decoder.texels,Is.EqualTo(4));
                var p=texture.GetPixels32();
                Assert.That(p[0],Is.EqualTo(new Color32(0,0,255,255))); // bottom left
                Assert.That(p[2],Is.EqualTo(new Color32(128,0,0,128))); // top left
                Assert.That(p[3],Is.EqualTo(new Color32(0,255,0,255)));
                Assert.That(p[1],Is.EqualTo(new Color32(0,0,0,0)));
            }
            finally { TextureDecoder.Destroy(texture); }
        }
        [Test]
        public void DimensionsAreLimitedBeforePngDecode()
        {
            var bytes=File.ReadAllBytes(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/colors.png"));
            bytes[16]=0;bytes[17]=0;bytes[18]=0x40;bytes[19]=0;
            Assert.That(Assert.Throws<LivLivError>(()=>new TextureDecoder().Decode(bytes)).code,Is.EqualTo("too-large"));
        }
        private sealed class HostWebP : IWebPDecoder
        {
            public int size=2,decodeCalls;
            public bool GetInfo(byte[] bytes,out int width,out int height) { width=height=size;return true; }
            public bool DecodeRgba(byte[] bytes,byte[] output,int width,int height)
            { decodeCalls++;for(int i=0;i<output.Length;i+=4){output[i]=255;output[i+1]=255;output[i+3]=255;}return true; }
        }
        [Test]
        public void HostCanSupplyAPlatformDecoder()
        {
            var host=new HostWebP();var bytes=File.ReadAllBytes(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/colors.webp"));
            var texture=new TextureDecoder(host).Decode(bytes);
            try { Assert.That(host.decodeCalls,Is.EqualTo(1));Assert.That(texture.GetPixels32()[0],Is.EqualTo(new Color32(255,255,0,255))); }
            finally { TextureDecoder.Destroy(texture); }
        }
        [Test]
        public void PlatformDecoderCannotBypassDimensionBudget()
        {
            var host=new HostWebP { size=10000 };var bytes=File.ReadAllBytes(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/colors.webp"));
            Assert.That(Assert.Throws<LivLivError>(()=>new TextureDecoder(host).Decode(bytes)).code,Is.EqualTo("too-large"));
            Assert.That(host.decodeCalls,Is.EqualTo(0));
        }
        private static byte[] Png(int w,int h,Func<int,int,Color32> pixel)
        {
            var t=new Texture2D(w,h,TextureFormat.RGBA32,false,true);var p=new Color32[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)p[(h-y-1)*w+x]=pixel(x,y);
            t.SetPixels32(p);t.Apply();var bytes=t.EncodeToPNG();TextureDecoder.Destroy(t);return bytes;
        }
        private static JObject Layer(string id,int x,int y,int w,int h,string role=null,string clip=null)
        {
            var l=new JObject { ["id"]=id,["texture"]="textures/"+id+".png",["x"]=x,["y"]=y,["w"]=w,["h"]=h,["group"]="head" };
            if(role!=null)l["role"]=role;if(clip!=null)l["clip"]=clip;return l;
        }
        private static Dictionary<string,byte[]> Synthetic()
        {
            var eye=Layer("eye",40,8,16,16,"eye_R");eye["mask"]="textures/mask.png";
            var m=new JObject
            {
                ["format"]="livliv-model",["version"]=4,["name"]="Synthetic compositor test",["canvas"]=new JArray(64,64),
                ["anchors"]=new JObject { ["head"]=new JObject { ["cx"]=32,["cy"]=20,["rx"]=12,["ry"]=14 },["face"]=new JArray(20,8,44,34),["neck_y"]=34,["top_y"]=0,["bottom_y"]=63,["hair_root_y"]=6 },
                ["layers"]=new JArray(Layer("base",0,0,64,64),Layer("green",0,0,32,32),eye,Layer("iris",40,8,16,16,"iris_R","eye"))
            };
            return new Dictionary<string,byte[]>
            {
                ["model.json"]=Encoding.UTF8.GetBytes(m.ToString()),["textures/base.png"]=Png(64,64,(x,y)=>new Color32(255,0,0,128)),
                ["textures/green.png"]=Png(32,32,(x,y)=>new Color32(0,255,0,128)),["textures/eye.png"]=Png(16,16,(x,y)=>new Color32(0,0,0,1)),
                ["textures/iris.png"]=Png(16,16,(x,y)=>new Color32(0,0,255,255)),["textures/mask.png"]=Png(16,16,(x,y)=>new Color32(255,255,255,(byte)(x>=4&&x<12&&y>=4&&y<12?255:0)))
            };
        }
        private static Texture2D Read(RenderTexture target)
        {
            var old=RenderTexture.active;
            try { RenderTexture.active=target;var t=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);t.ReadPixels(new Rect(0,0,target.width,target.height),0,0);t.Apply();return t; }
            finally { RenderTexture.active=old; }
        }
        private static void Pixel(Texture2D t,int x,int y,Color32 expected)
        {
            Color32 actual=t.GetPixel(x,t.height-1-y);
            Assert.That(actual.r,Is.EqualTo(expected.r).Within(1),$"({x},{y}) R");Assert.That(actual.g,Is.EqualTo(expected.g).Within(1),$"({x},{y}) G");
            Assert.That(actual.b,Is.EqualTo(expected.b).Within(1),$"({x},{y}) B");Assert.That(actual.a,Is.EqualTo(expected.a).Within(1),$"({x},{y}) A");
        }
        [Test]
        public void PlayerHostReloadControlsAndTargetOwnership()
        {
            var files=Synthetic();var package=ModelPackage.FromFiles(files);
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            try
            {
                using(var player=new LivLiv.Unity.Player(target,new PlayerOptions{autoplay=false,idle=false}))
                {
                    player.Renderer.Core.procJaw=0;player.Load(package);
                    Assert.That(player.Info.name,Is.EqualTo("Synthetic compositor test"));
                    var pose=player.Renderer.Core.@params;player.@params.angleX=12;player.Play();player.Tick(.02);
                    Assert.That(pose.angleX,Is.EqualTo(12));Assert.That(player.Renderer.Core.time,Is.EqualTo(.02));
                    player.Pause();player.Tick(.2);Assert.That(player.Renderer.Core.time,Is.EqualTo(.02));
                    player.view.x=.25;player.Resize();var moved=player.CanvasToModel(32,32);player.view.x=0;player.Resize();var center=player.CanvasToModel(32,32);
                    Assert.That(moved[0],Is.LessThan(center[0]));Assert.That(moved[1],Is.EqualTo(center[1]));
                    player.Renderer.Core.hidden.Add("green");player.Load(package);
                    Assert.That(player.Renderer.Core.@params,Is.SameAs(pose));Assert.That(player.Renderer.Core.procJaw,Is.Zero);
                    Assert.That(player.Renderer.Core.time,Is.EqualTo(.02));Assert.That(player.Renderer.Core.hidden.Contains("green"),Is.True);
                    var core=player.Renderer.Core;files.Remove("textures/green.png");Assert.Throws<LivLivError>(()=>player.Load(ModelPackage.FromFiles(files)));
                    Assert.That(player.Renderer.Core,Is.SameAs(core));player.Resize();
                    player.Dispose();Assert.That(target.IsCreated(),Is.True);Assert.Throws<ObjectDisposedException>(()=>player.Load(package));
                }
            }
            finally { target.Release();TextureDecoder.Destroy(target); }
        }
        [Test]
        public void NeutralCompositorDrawOrderAlphaStencilAndTransactionalReload()
        {
            var files=Synthetic();var package=ModelPackage.FromFiles(files);
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();Texture2D image=null;
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(package);renderer.RenderNeutral(target);image=Read(target);
                    var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults/neutral-compositor.png"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
                    Pixel(image,8,8,new Color32(64,128,0,192));Pixel(image,56,56,new Color32(128,0,0,128));
                    Pixel(image,48,16,new Color32(0,0,255,255));Pixel(image,41,9,new Color32(128,0,0,128));
                    files.Remove("textures/green.png");
                    Assert.That(Assert.Throws<LivLivError>(()=>renderer.Load(ModelPackage.FromFiles(files))).code,Is.EqualTo("missing-file"));
                    Assert.That(renderer.Model,Is.SameAs(package.model));renderer.RenderNeutral(target);
                    var second=Read(target);try { CollectionAssert.AreEqual(image.GetPixels32(),second.GetPixels32()); } finally { TextureDecoder.Destroy(second); }
                }
            }
            finally { TextureDecoder.Destroy(image);target.Release();TextureDecoder.Destroy(target); }
        }
        [Test]
        public void AnimatedSoftMaskPreservesFractionalEyeCoverage()
        {
            var files=Synthetic();files["textures/mask.png"]=Png(16,16,(x,y)=>new Color32(255,255,255,(byte)(x>=4&&x<12&&y>=4&&y<12?128:0)));
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(ModelPackage.FromFiles(files));renderer.Core.layers.First(l=>(string)l.def["id"]=="iris").soft=1;renderer.Render(target);
                    var image=Read(target);try{Pixel(image,48,16,new Color32(64,0,128,192));Pixel(image,41,9,new Color32(128,0,0,128));}finally{TextureDecoder.Destroy(image);}
                    renderer.SoftIris=false;renderer.Render(target);image=Read(target);try{Pixel(image,48,16,new Color32(0,0,255,255));}finally{TextureDecoder.Destroy(image);}
                }
            }
            finally{target.Release();TextureDecoder.Destroy(target);}
        }
        [Test]
        public void TalkingMouthDrawsAboveExpressionUntilTalkLatchExpires()
        {
            var files=Synthetic();var model=JObject.Parse(Encoding.UTF8.GetString(files["model.json"]));var mouth=Layer("mouth",0,32,16,16,"mouth");var expression=Layer("expression",0,32,16,16,"expr");
            mouth["frames"]=new JArray("textures/closed.png","textures/half.png","textures/open.png","textures/round.png","textures/wide.png","textures/shut.png");mouth["visemes"]=new JArray("closed","half","open","round","wide","shut");expression["param"]="smile";expression["talk"]="textures/talk.png";
            ((JArray)model["layers"]).Add(mouth);((JArray)model["layers"]).Add(expression);files["model.json"]=Encoding.UTF8.GetBytes(model.ToString());
            files["textures/mouth.png"]=files["textures/closed.png"]=Png(16,16,(x,y)=>new Color32(255,0,0,255));
            foreach(string path in new[]{"textures/half.png","textures/open.png","textures/round.png","textures/wide.png"})files[path]=Png(16,16,(x,y)=>new Color32(0,0,255,255));
            files["textures/shut.png"]=Png(16,16,(x,y)=>new Color32(255,255,0,255));files["textures/expression.png"]=Png(16,16,(x,y)=>new Color32(0,255,255,255));files["textures/talk.png"]=Png(16,16,(x,y)=>new Color32(0,255,0,255));
            var target=new RenderTexture(64,64,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(ModelPackage.FromFiles(files));renderer.Core.@params.smile=1;renderer.Core.@params.mouthOpen=.8;renderer.Update(.2);renderer.Render(target);
                    var image=Read(target);try{Pixel(image,8,40,new Color32(0,0,255,255));}finally{TextureDecoder.Destroy(image);}
                    renderer.Core.@params.mouthOpen=0;renderer.Update(.07);renderer.Render(target);Assert.That(renderer.Core.mouthFrame,Is.EqualTo(5));
                    image=Read(target);try{Pixel(image,8,40,new Color32(255,255,0,255));}finally{TextureDecoder.Destroy(image);}
                    for(int i=0;i<8;i++)renderer.Update(.1);renderer.Render(target);Assert.That(renderer.Core.talkLeft,Is.EqualTo(0));
                    image=Read(target);try{Pixel(image,8,40,new Color32(0,255,255,255));}finally{TextureDecoder.Destroy(image);}
                }
            }
            finally{target.Release();TextureDecoder.Destroy(target);}
        }
        [Test]
        public void AllFeaturedCharacterImagesDecodeAndDispose()
        {
            int before=Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name=="LivLiv decoded texture");
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters"));int count=0;
            using(var renderer=new PuppetRenderer())
            foreach(var dir in Directory.GetDirectories(root).OrderBy(p=>p,StringComparer.Ordinal))
            {
                if(!File.Exists(Path.Combine(dir,"model.json")))continue;
                renderer.Load(ModelPackage.FromDirectory(dir));
                Assert.That(renderer.Texels,Is.GreaterThan(0),dir);count++;
            }
            Assert.That(count,Is.EqualTo(21));
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name=="LivLiv decoded texture"),Is.EqualTo(before),"Decoded GPU textures survived Dispose");
        }
        public static IEnumerable<TestCaseData> AlphaModels()=>JObject.Parse(File.ReadAllText(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/alpha-reference.json")))["models"].Select(m=>new TestCaseData((string)m["id"]).SetName("Canvas alpha: "+m["id"]));
        [TestCaseSource(nameof(AlphaModels))]
        public void AlphaGridMatchesBrowserCanvas(string id)
        {
            var fixture=JObject.Parse(File.ReadAllText(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/alpha-reference.json")));
            var reference=fixture["models"].First(m=>(string)m["id"]==id);var errors=new List<string>();
            var package=ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters",id)));var decoder=new TextureDecoder();
            foreach(var expected in reference["layers"])
            {
                var def=((JArray)package.model["layers"]).Cast<JObject>().First(l=>(string)l["id"]==(string)expected["id"]);var textures=new List<Texture2D>();
                try
                {
                    textures.Add(decoder.Decode(package.ReadImage(def,(string)def["texture"])));
                    foreach(var path in def["frames"] as JArray??new JArray())textures.Add(decoder.Decode(package.ReadImage(def,(string)path)));
                    var grid=new LivLiv.Runtime.LayerLattice(package.model,def);
                    var actual=LivLiv.Runtime.Puppet.AlphaGrid(LayerSampling.Grid(textures[0],textures.Skip(1).ToArray(),grid),grid.cols,grid.rows);
                    var wanted=Convert.FromBase64String((string)expected["occ"]);Assert.That(actual.Length,Is.EqualTo(wanted.Length));
                    int changed=actual.Where((v,i)=>v!=wanted[i]).Count();if(changed!=0)errors.Add((string)def["id"]+": "+changed+"/"+wanted.Length+" actual "+actual.Sum(v=>(int)v)+" wanted "+wanted.Sum(v=>(int)v));
                }
                finally {foreach(var texture in textures)TextureDecoder.Destroy(texture);}
            }
            Assert.That(errors,Is.Empty,string.Join("; ",errors));
        }
        [Test]
        public void BrowserAlphaFixtureUsesCurrentSources()
        {
            var fixture=JObject.Parse(File.ReadAllText(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/alpha-reference.json")));
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var p in ((JObject)fixture["sourceHashes"]).Properties())
                Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value),p.Name);
        }
        [TestCase("d01")]
        [TestCase("char06")]
        public void FeaturedNeutralFrameExportsWithPackageWatermark(string id)
        {
            var target=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();Texture2D image=null;
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters",id))));renderer.RenderNeutral(target);
                    image=Read(target);Assert.That(image.GetPixels32().Count(c=>c.a>0),Is.GreaterThan(10000),"Character did not render");
                    var output=Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults/neutral-"+id+".png"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
                }
            }
            finally {TextureDecoder.Destroy(image);target.Release();TextureDecoder.Destroy(target);}
        }
        [Test]
        public void DecodedLashCurveMatchesBrowser()
        {
            var file=Path.Combine(PackageRoot,"Tests/Editor/Fixtures/lash.json.gz");JArray rows;
            using(var stream=File.OpenRead(file))using(var zip=new System.IO.Compression.GZipStream(stream,System.IO.Compression.CompressionMode.Decompress))using(var reader=new StreamReader(zip))rows=(JArray)JObject.Parse(reader.ReadToEnd())["samples"];
            var errors=new List<string>();
            foreach(var row in rows)
            {
                var package=ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters",(string)row["model"])));var def=(JObject)row["def"];byte[] actual=null;
                var texture=new TextureDecoder().Decode(package.ReadImage(def,(string)def["texture"]),(p,w,h,isWebP)=>actual=LayerSampling.CanvasRgba(p,w,h,isWebP));TextureDecoder.Destroy(texture);
                string id=(string)row["model"]+"-"+(string)def["id"];
                var curve=LivLiv.Runtime.Puppet.ClosedLashLine(actual,(int)row["width"],(int)row["height"],def);
                Assert.That(curve==null,Is.EqualTo(row["expected"].Type==JTokenType.Null),id);
                if(curve!=null)for(int i=0;i<((JArray)row["xs"]).Count;i++)Assert.That(curve((double)row["xs"][i]),Is.EqualTo((double)row["expected"][i]).Within(1e-9),id);
            }
            Assert.That(errors,Is.Empty,string.Join("; ",errors));
        }
        public static IEnumerable<TestCaseData> AnimatedCases()=>JObject.Parse(File.ReadAllText(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/animation-reference.json")))["cases"].Select(r=>new TestCaseData(r).SetName("Animated "+r["id"]+" "+r["name"]));
        [TestCaseSource(nameof(AnimatedCases))]
        public void AnimatedGeometryMatchesBrowserAndExportsFrame(JToken reference)
        {
            string id=(string)reference["id"],name=(string)reference["name"];var errors=new List<string>();
            var target=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(ModelPackage.FromDirectory(Path.GetFullPath(Path.Combine(Application.dataPath,"../../characters",id))));
                    foreach(var value in ((JObject)reference["params"]).Properties())renderer.Core.@params[value.Name]=(double)value.Value;
                    int frames=(int)reference["frames"];if(frames==0)renderer.Update(0);else for(int i=0;i<frames;i++)renderer.Update(1d/60);
                    renderer.Render(target);var image=Read(target);
                    try {Assert.That(image.GetPixels32().Count(c=>c.a>0),Is.GreaterThan(10000));File.WriteAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../TestResults/animated-"+id+"-"+name+".png")),image.EncodeToPNG());}
                    finally{TextureDecoder.Destroy(image);}
                    void Compare(double actual,double expected,string field){if(Math.Abs(actual-expected)>1e-6*Math.Max(1,Math.Abs(expected)))errors.Add(field+": "+actual+" vs "+expected);}
                    foreach(var wanted in reference["layers"])
                    {
                        var layer=renderer.Core.layers.First(l=>(string)l.def["id"]==(string)wanted["id"]);string path=(string)wanted["id"];
                        Assert.That(layer.rest.Length/2,Is.EqualTo((int)wanted["count"]),path);int sample=0;
                        foreach(int index in wanted["indices"])
                        {
                            if((bool)wanted["shown"]){Compare(layer.output[2*index],(double)wanted["output"][2*sample],path+".x["+index+"]");Compare(layer.output[2*index+1],(double)wanted["output"][2*sample+1],path+".y["+index+"]");}
                            foreach(var field in new[]{"fol","folB","pivY"})
                            {
                                var actual=field=="fol"?layer.fol:field=="folB"?layer.folB:layer.pivY;
                                Assert.That(actual==null,Is.EqualTo(wanted[field].Type==JTokenType.Null),path+"."+field);
                                if(actual!=null)Compare(actual[index],(double)wanted[field][sample],path+"."+field+"["+index+"]");
                            }
                            sample++;
                        }
                        if(wanted["pivC"].Type!=JTokenType.Null)Compare(layer.pivC.Value,(double)wanted["pivC"],path+".pivC");Compare(layer.soft,(double)wanted["soft"],path+".soft");
                    }
                    foreach(var entry in ((JObject)reference["nasal"]).Properties())
                    {
                        Assert.That(renderer.Core.nasal.ContainsKey(entry.Name),Is.True,entry.Name);var actual=JObject.FromObject(renderer.Core.nasal[entry.Name]);
                        foreach(var value in ((JObject)entry.Value).Properties())Compare((double)actual[value.Name],(double)value.Value,"nasal."+entry.Name+"."+value.Name);
                    }
                }
            }
            finally{target.Release();TextureDecoder.Destroy(target);}
            Assert.That(errors,Is.Empty,errors.Count+" differences; "+string.Join("; ",errors.Take(20)));
        }
        [Test]
        public void BrowserAnimationFixtureUsesCurrentSources()
        {
            var fixture=JObject.Parse(File.ReadAllText(Path.Combine(PackageRoot,"Tests/Graphics/Fixtures/animation-reference.json")));var root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            using(var sha=System.Security.Cryptography.SHA256.Create())foreach(var p in ((JObject)fixture["sourceHashes"]).Properties())Assert.That(BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root,p.Name)))).Replace("-","").ToLowerInvariant(),Is.EqualTo((string)p.Value),p.Name);
        }
        [Test]
        public void WatermarkComposesAfterModelAndTracksBlinkAndConfiguration()
        {
            var files=Synthetic();var m=JObject.Parse(Encoding.UTF8.GetString(files["model.json"]));m["meta"]=new JObject { ["watermark"]=new JObject { ["kind"]="livliv" } };files["model.json"]=Encoding.UTF8.GetBytes(m.ToString());
            var target=new RenderTexture(512,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
            int before=Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name=="LivLiv watermark");
            try
            {
                using(var renderer=new PuppetRenderer())
                {
                    renderer.Load(ModelPackage.FromFiles(files));renderer.RenderNeutral(target);
                    var image=Read(target);Color32[] open;
                    try {open=image.GetPixels32();Assert.That(open.Count(c=>c.r>200&&c.g>100&&c.b>100),Is.GreaterThan(20));}
                    finally {TextureDecoder.Destroy(image);}
                    renderer.RenderNeutral(target,5252.5);image=Read(target);
                    try {Assert.That(image.GetPixels32(),Is.Not.EqualTo(open),"Blink must alter watermark pixels");}
                    finally {TextureDecoder.Destroy(image);}
                    m["meta"]["watermark"]["kind"]="text";m["meta"]["watermark"]["text"]="Owner name";files["model.json"]=Encoding.UTF8.GetBytes(m.ToString());renderer.Load(ModelPackage.FromFiles(files));
                    renderer.RenderNeutral(target);image=Read(target);
                    try{Assert.That(image.GetPixels32().Count(c=>c.r>180&&c.g>180&&c.b>180),Is.GreaterThan(20),"Owner text must be composited");}finally{TextureDecoder.Destroy(image);}
                }
            }
            finally {target.Release();TextureDecoder.Destroy(target);}
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>t.name=="LivLiv watermark"),Is.EqualTo(before));
        }
        [TestCase(512,768,0,true)]
        [TestCase(768,512,5252.5,true)]
        [TestCase(512,768,0,false)]
        public void WatermarkGpuMatchesPortableRasterAndExportsReference(int w,int h,double ms,bool center)
        {
            var target=new RenderTexture(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();Texture2D image=null;
            try
            {
                using(var mark=new UnityMark())using(var cmd=new UnityEngine.Rendering.CommandBuffer())
                {
                    mark.Set("livliv",null,center);mark.Prepare(w,h,ms);cmd.SetRenderTarget(target);cmd.ClearRenderTarget(true,true,Color.clear);mark.Draw(cmd);
                    bool old=GL.sRGBWrite;try {GL.sRGBWrite=false;Graphics.ExecuteCommandBuffer(cmd);}finally {GL.sRGBWrite=old;}
                    image=Read(target);
                    var core=new LivLiv.Mark();core.Set("livliv",null,center);var expected=BrandRasterizer.Draw(w,h,core.Draw(w,h,ms));
                    var pixels=image.GetPixels32();for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                    {
                        var c=pixels[(h-y-1)*w+x];int p=(y*w+x)*4;
                        if(Math.Abs(c.r-expected[p])>1||Math.Abs(c.g-expected[p+1])>1||Math.Abs(c.b-expected[p+2])>1||Math.Abs(c.a-expected[p+3])>1)Assert.Fail($"Watermark GPU pixel ({x},{y}) differs");
                    }
                    var output=Path.GetFullPath(Path.Combine(Application.dataPath,$"../TestResults/mark-{w}-{h}-{(center?"center":"corner")}.png"));Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllBytes(output,image.EncodeToPNG());
                }
            }
            finally {TextureDecoder.Destroy(image);target.Release();TextureDecoder.Destroy(target);}
        }
    }
}
