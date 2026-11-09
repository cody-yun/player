using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using LivLiv.Runtime;
using LivLiv.Unity;
using LivLiv.Unity.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace LivLiv.Tests
{
    public sealed class PosterPresenterTests
    {
        private sealed class Host:PlayerBase
        {
            public bool ready;private readonly Dictionary<string,double> expressions=new Dictionary<string,double>();
            public Host():base(new Params(),new PlayerOptions{idle=false,autoplay=false}){}
            public Task<int> Load(string poster)=>StartLoad(new LoadOptions{poster=poster},j=>Task.FromResult(1));
            protected override bool Drawable=>ready;protected override double Clock=>0;
            protected override IDictionary<string,double> ShownExpressions=>expressions;
            protected override IEnumerable<string> ExpressionIds=>Enumerable.Empty<string>();
            protected override Puppet Seen=>null;protected override double DisplayWidth=>100;protected override double DisplayHeight=>100;
            protected override void Show(double dt){}protected override void Free(){}
        }
        private sealed class Transport:IModelTransport
        {
            public readonly List<TaskCompletionSource<ModelResponse>> requests=new List<TaskCompletionSource<ModelResponse>>();
            public readonly List<CancellationToken> tokens=new List<CancellationToken>();public readonly List<Uri> urls=new List<Uri>();
            public Task<ModelResponse> Get(Uri uri,int cap,bool noCache,CancellationToken token,Action<double> progress=null)
            {var result=new TaskCompletionSource<ModelResponse>();requests.Add(result);tokens.Add(token);urls.Add(uri);return result.Task;}
        }
        private GameObject canvas;private RawImage image;private PosterPresenter presenter;private Host host;
        private Texture2D original;private Vector2 originalSize;
        private static byte[] Fixture(string kind)=>File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../Packages/ai.livliv.player/Tests/Graphics/Fixtures/colors."+kind)));
        private static string DataUrl(string kind)=>"data:image/"+kind+";base64,"+Convert.ToBase64String(Fixture(kind));
        [UnitySetUp] public IEnumerator Setup()
        {
            canvas=new GameObject("Poster test",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var parent=new GameObject("bounds",typeof(RectTransform));parent.transform.SetParent(canvas.transform,false);
            ((RectTransform)parent.transform).sizeDelta=new Vector2(240,120);
            var child=new GameObject("poster",typeof(RectTransform),typeof(RawImage),typeof(AspectRatioFitter));child.transform.SetParent(parent.transform,false);
            image=child.GetComponent<RawImage>();original=new Texture2D(1,1);image.texture=original;image.enabled=false;
            originalSize=image.rectTransform.sizeDelta;presenter=child.AddComponent<PosterPresenter>();host=new Host();presenter.Bind(host);
            yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            presenter.Unbind();host.Dispose();UnityEngine.Object.Destroy(canvas);UnityEngine.Object.Destroy(original);yield return null;
        }
        [UnityTest] public IEnumerator PngAndWebpFitContainAndFirstFrameRestoresCallerBackground()
        {
            foreach(string kind in new[]{"png","webp"})
            {
                host.ready=false;host.Load(DataUrl(kind));yield return null;Canvas.ForceUpdateCanvases();
                Assert.That(presenter.LastError,Is.Null);Assert.That(presenter.Showing,Is.True);Assert.That(image.enabled,Is.True);
                Assert.That(image.rectTransform.rect.width,Is.EqualTo(120).Within(.1));Assert.That(image.rectTransform.rect.height,Is.EqualTo(120).Within(.1));
                var texture=(Texture2D)image.texture;Assert.That(texture.GetPixels32()[2].r,Is.EqualTo(255));
                host.ready=true;host.Resize();yield return null;
                Assert.That(presenter.Showing,Is.False);Assert.That(image.texture,Is.SameAs(original));Assert.That(image.enabled,Is.False);
                Assert.That(image.rectTransform.sizeDelta,Is.EqualTo(originalSize));Assert.That(texture==null,Is.True);Assert.That(original!=null,Is.True);
            }
        }
        [UnityTest] public IEnumerator LateDownloadCannotReplaceNewerPoster()
        {
            var transport=new Transport();presenter.Bind(host,new Uri("https://example.test/models/"),transport);
            host.Load("old.webp");host.Load("new.png");
            Assert.That(transport.tokens[0].IsCancellationRequested,Is.True);Assert.That(transport.urls[1].AbsoluteUri,Is.EqualTo("https://example.test/models/new.png"));
            transport.requests[1].SetResult(new ModelResponse{bytes=Fixture("png")});yield return null;var current=image.texture;
            transport.requests[0].SetResult(new ModelResponse{bytes=Fixture("webp")});yield return null;
            Assert.That(image.texture,Is.SameAs(current));Assert.That(presenter.LastError,Is.Null);Assert.That(presenter.Loading,Is.False);
        }
        [UnityTest] public IEnumerator JpegDataUrlFitsAndReleasesOnFirstFrame()
        {
            var bytes=File.ReadAllBytes(Path.GetFullPath(Path.Combine(Application.dataPath,"../Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-progressive.jpg")));
            host.Load("data:image/jpeg;base64,"+Convert.ToBase64String(bytes));yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(presenter.LastError,Is.Null);Assert.That(presenter.Showing,Is.True);
            var texture=(Texture2D)image.texture;Assert.That(texture.width,Is.EqualTo(32));Assert.That(texture.height,Is.EqualTo(48));
            Assert.That(image.rectTransform.rect.height,Is.EqualTo(120).Within(.1));Assert.That(image.rectTransform.rect.width,Is.EqualTo(80).Within(.1));
            host.ready=true;host.Resize();yield return null;
            Assert.That(texture==null,Is.True);Assert.That(presenter.Showing,Is.False);Assert.That(image.texture,Is.SameAs(original));
        }
        [UnityTest] public IEnumerator DisableCancelsAndEnableResumesCurrentPosterWithoutOwningPlayer()
        {
            var transport=new Transport();presenter.Bind(host,new Uri("https://example.test/"),transport);host.Load("a.png");
            presenter.enabled=false;Assert.That(transport.tokens[0].IsCancellationRequested,Is.True);
            transport.requests[0].SetResult(new ModelResponse{bytes=Fixture("png")});yield return null;
            Assert.That(presenter.Showing,Is.False);Assert.That(image.texture,Is.SameAs(original));Assert.That(host.IsDisposed,Is.False);
            presenter.enabled=true;Assert.That(transport.requests.Count,Is.EqualTo(2));transport.requests[1].SetResult(new ModelResponse{bytes=Fixture("png")});yield return null;
            Assert.That(presenter.Showing,Is.True);presenter.Unbind();yield return null;Assert.That(host.Poster,Is.EqualTo("a.png"));
        }
        [UnityTest] public IEnumerator InvalidPosterDoesNotFailModelAndRetrySucceeds()
        {
            int errors=0;presenter.Failed+=s=>errors++;
            var model=host.Load("data:image/png;base64,AAAA");yield return null;
            Assert.That(model.IsCompletedSuccessfully,Is.True);Assert.That(errors,Is.EqualTo(1));Assert.That(presenter.Showing,Is.False);Assert.That(image.texture,Is.SameAs(original));
            host.Load(DataUrl("png"));yield return null;Assert.That(presenter.LastError,Is.Null);Assert.That(presenter.Showing,Is.True);
        }
        [UnityTest] public IEnumerator DefaultFileTransportDecodesAndDisposeFreesOwnedTexture()
        {
            host.Load(new Uri(Path.GetFullPath(Path.Combine(Application.dataPath,"../Packages/ai.livliv.player/Tests/Graphics/Fixtures/colors.webp"))).AbsoluteUri);
            double deadline=Time.realtimeSinceStartupAsDouble+10;
            while(presenter.Loading&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(presenter.LastError,Is.Null);Assert.That(presenter.Showing,Is.True);
            var texture=image.texture;host.Dispose();yield return null;
            Assert.That(texture==null,Is.True);Assert.That(image.texture,Is.SameAs(original));
        }
    }
}
