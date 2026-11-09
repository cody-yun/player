using System;
using System.IO;
using NUnit.Framework;
using LivLiv.Unity.MediaPipe;
using Mediapipe.Tasks.Vision.FaceLandmarker;
using Mediapipe.Tasks.Components.Containers;
using UnityEngine;
namespace LivLiv.MediaPipe.Tests
{
    public sealed class NativeFaceTests
    {
        private sealed class Source:INativeFaceSource,LivLiv.Platform.ICaptureStatus
        {
            public Texture2D texture;public bool fresh=true;public int disposed;
            public bool available=true;public string Error {get;set;}
            public bool Ready=>disposed==0&&available;
            public bool TryRead(out Texture2D value){value=texture;bool yes=fresh;fresh=false;return yes;}
            public void Dispose(){disposed++;}
        }
        [Test]public void SourceFailureReleasesTaskAndSourceWithoutWaitingForInference()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            var source=new Source{fresh=false,available=false};
            using var input=new NativeFaceInput(File.ReadAllBytes(Path.Combine(root,"unity/Packages/ai.livliv.player/Browser~/face_landmarker.task")),source);
            Assert.That(input.Ready,Is.False);Assert.That(input.Error,Is.Null,"temporary unready source is not terminal");
            Assert.That(source.disposed,Is.Zero);source.Error="Camera capture ended";
            Assert.That(input.Error,Is.EqualTo(source.Error));Assert.That(source.disposed,Is.EqualTo(1));
            Assert.That(input.Ready,Is.False);Assert.That(input.Next(1000),Is.Null);
            input.Dispose();Assert.That(source.disposed,Is.EqualTo(1));Assert.That(input.Error,Is.EqualTo(source.Error));
        }
        [TestCase(0,false,new byte[]{4,5,6,1,2,3})]
        [TestCase(90,false,new byte[]{1,4,2,5,3,6})]
        [TestCase(180,false,new byte[]{3,2,1,6,5,4})]
        [TestCase(270,false,new byte[]{6,3,5,2,4,1})]
        [TestCase(0,true,new byte[]{1,2,3,4,5,6})]
        [TestCase(90,true,new byte[]{4,1,5,2,6,3})]
        [TestCase(180,true,new byte[]{6,5,4,3,2,1})]
        [TestCase(270,true,new byte[]{3,6,2,5,1,4})]
        public void PixelsBecomeUprightTopDown(int angle,bool mirror,byte[] expected)
        {
            var src=new Color32[6];var dst=new Color32[6];for(byte i=0;i<6;i++)src[i]=new Color32((byte)(i+1),0,0,255);
            WebCamFaceSource.Orient(src,3,2,mirror,angle,dst);for(int i=0;i<6;i++)Assert.AreEqual(expected[i],dst[i].r);
        }
        [Test]public void CancelledStartDoesNotOpenCamera()
        {Assert.CatchAsync<OperationCanceledException>(async()=>await WebCamFaceSource.StartAsync(new byte[]{1},cancellationToken:new System.Threading.CancellationToken(true)));}
        [Test]public void MappingUndoesPluginZConversionAndKeepsColumnOrder()
        {
            var result=FaceLandmarkerResult.Alloc(1,true,true);var landmarks=NormalizedLandmarks.Alloc(153);
            for(int i=0;i<153;i++)landmarks.landmarks.Add(NormalizedLandmark.CreateFrom(new Mediapipe.NormalizedLandmark{X=i/200f,Y=i/300f}));result.faceLandmarks.Add(landmarks);
            var matrix=new Matrix4x4();for(int c=0;c<4;c++)for(int r=0;r<4;r++)matrix[r,c]=(c*4+r+1)*((r==2?-1:1)*(c==2?-1:1));result.facialTransformationMatrixes.Add(matrix);
            var categories=new Mediapipe.ClassificationList();categories.Classification.Add(new Mediapipe.Classification{Label="jawOpen",Score=.7f});categories.Classification.Add(new Mediapipe.Classification{Label="eyeBlinkLeft",Score=.2f});result.faceBlendshapes.Add(Classifications.CreateFrom(categories));
            var frame=NativeFaceInput.ToFrame(result,123);for(int i=0;i<16;i++)Assert.AreEqual(i+1,frame.m[i]);Assert.AreEqual(.7,frame.b[2],1e-6);Assert.AreEqual(.2,frame.b[0],1e-6);Assert.AreEqual(0,frame.b[1]);Assert.AreEqual(152/200f,frame.l[6]);
            result.faceLandmarks.Clear();Assert.IsNull(NativeFaceInput.ToFrame(result,124).m);
        }
        [Test]public void ActualCpuInferenceDetectsPortraitThenLosesFaceAndReleases()
        {
            string root=Path.GetFullPath(Path.Combine(Application.dataPath,"../.."));
            byte[] model=File.ReadAllBytes(Path.Combine(root,"unity/Packages/ai.livliv.player/Browser~/face_landmarker.task"));
            var photo=new Texture2D(2,2,TextureFormat.RGBA32,false,true);var inputTexture=new Texture2D(640,480,TextureFormat.RGBA32,false,true);
            Source source=null;NativeFaceInput input=null;
            try
            {
                Assert.IsTrue(ImageConversion.LoadImage(photo,File.ReadAllBytes(Path.Combine(root,"unity/TestResults/mediapipe/portrait.jpg"))));
                var pixels=photo.GetPixels32();var top=new Color32[640*480];
                // Match browser drawImage(photo,0,0,820,615,0,0,640,480), nearest CPU sampling.
                for(int y=0;y<480;y++)for(int x=0;x<640;x++)top[y*640+x]=pixels[(photo.height-1-y*615/480)*photo.width+x*820/640];
                var portrait=(Color32[])top.Clone();inputTexture.SetPixels32(top);source=new Source{texture=inputTexture};input=new NativeFaceInput(model,source,0);
                var frame=input.Next(1000);Assert.IsNull(input.Error,input.Error);Assert.NotNull(frame);Assert.NotNull(frame.m,"CPU found no face");Assert.AreEqual(18,frame.b.Length);Assert.AreEqual(8,frame.l.Length);
                Assert.Greater(frame.m[0],.7);Assert.Greater(frame.m[5],.7);Assert.Greater(frame.l[6],0);Assert.Less(frame.l[6],1);
                File.WriteAllText(Path.Combine(root,"unity/TestResults/native-face-frame.json"),JsonUtility.ToJson(frame,true));
                Assert.IsNull(input.Next(1000));Assert.IsNull(input.Next(1001));
                for(int i=0;i<top.Length;i++)top[i]=new Color32(0,0,0,255);inputTexture.SetPixels32(top);source.fresh=true;
                var empty=input.Next(2200);Assert.NotNull(empty);Assert.IsNull(empty.m);Assert.IsTrue(input.Dark);
                input.Dispose();input.Dispose();Assert.AreEqual(1,source.disposed);Assert.IsFalse(input.Ready);Assert.IsNull(input.Next(2300));Assert.IsTrue(inputTexture!=null,"external source retains its texture");
                inputTexture.SetPixels32(portrait);source=new Source{texture=inputTexture};input=new NativeFaceInput(model,source,0);
                Assert.NotNull(input.Next(3000)?.m,"new native task must detect after previous task disposal");
            }
            finally{input?.Dispose();UnityEngine.Object.DestroyImmediate(photo);UnityEngine.Object.DestroyImmediate(inputTexture);}
        }
    }
}
