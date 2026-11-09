// Surface adapter for src/base.ts setPoster CSS background (center / contain / no-repeat).
// Put this RectTransform behind the character RawImage, in the same display bounds.
using System;
using System.Threading;
using System.Threading.Tasks;
using LivLiv.Platform;
using UnityEngine;
using UnityEngine.UI;

namespace LivLiv.Unity.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RawImage),typeof(AspectRatioFitter))]
    public sealed class PosterPresenter : MonoBehaviour
    {
        private RawImage image;
        private AspectRatioFitter fit;
        private PlayerBase player;
        private IModelTransport transport;
        private Uri baseUri;
        private CancellationTokenSource pending;
        private Texture2D owned;
        private Texture original;
        private bool originalEnabled,subscribed;
        private float originalAspect;
        private AspectRatioFitter.AspectMode originalMode;
        private bool initialized;
        private Vector2 originalMin,originalMax,originalPivot,originalPosition,originalSize;
        private int generation;
        private const int MaxBytes=32*1024*1024;
        public string LastError { get; private set; }
        public bool Loading=>pending!=null;
        public bool Showing=>owned!=null;
        public event Action<string> Failed;

        private void Awake()=>Initialize();
        private void Initialize()
        {
            if(initialized)return;initialized=true;
            image=GetComponent<RawImage>();fit=GetComponent<AspectRatioFitter>();
            original=image.texture;originalEnabled=image.enabled;originalAspect=fit.aspectRatio;originalMode=fit.aspectMode;
            var rect=image.rectTransform;originalMin=rect.anchorMin;originalMax=rect.anchorMax;originalPivot=rect.pivot;
            originalPosition=rect.anchoredPosition;originalSize=rect.sizeDelta;
        }
        /// <summary>Bind without taking ownership of the Player. Native relative URLs require baseUrl.</summary>
        public void Bind(PlayerBase source,Uri baseUrl=null,IModelTransport downloader=null)
        {
            Initialize();
            Unbind();player=source;baseUri=baseUrl;transport=downloader??new UnityModelTransport();
            if(baseUri==null&&Uri.TryCreate(Application.absoluteURL,UriKind.Absolute,out var page))baseUri=page;
            if(isActiveAndEnabled)Subscribe();
        }
        public void Unbind(){Unsubscribe();player=null;CancelAndRestore();LastError=null;}
        private void OnEnable(){Subscribe();}
        private void OnDisable(){Unsubscribe();CancelAndRestore();}
        private void OnDestroy(){Unbind();Failed=null;}
        private void Subscribe()
        {
            if(player==null||subscribed)return;
            player.PosterChanged+=Changed;subscribed=true;Changed(player.Poster);
        }
        private void Unsubscribe(){if(subscribed&&player!=null)player.PosterChanged-=Changed;subscribed=false;}
        private void CancelAndRestore()
        {
            generation++;var old=pending;pending=null;old?.Cancel(); // Async owner disposes its CTS in finally.
            if(image!=null)image.texture=original;
            if(owned!=null){TextureDecoder.Destroy(owned);owned=null;}
            if(image!=null)image.enabled=originalEnabled;
            if(fit!=null){fit.aspectMode=originalMode;fit.aspectRatio=originalAspect;}
            if(image!=null)
            {
                var rect=image.rectTransform;rect.anchorMin=originalMin;rect.anchorMax=originalMax;rect.pivot=originalPivot;
                rect.anchoredPosition=originalPosition;rect.sizeDelta=originalSize;
            }
        }
        private async void Changed(string ignored)
        {
            CancelAndRestore();LastError=null;
            string url=player?.Poster;if(string.IsNullOrEmpty(url)||!isActiveAndEnabled)return;
            var request=new CancellationTokenSource();pending=request;int mine=generation;Texture2D fresh=null;
            try
            {
                byte[] bytes;
                if(url.StartsWith("data:",StringComparison.OrdinalIgnoreCase))
                {
                    int comma=url.IndexOf(',');string header=comma<0?"":url.Substring(0,comma);
                    if(header!="data:image/png;base64"&&header!="data:image/webp;base64"&&header!="data:image/jpeg;base64")throw new ArgumentException("Poster data URL must be base64 PNG, WebP or JPEG");
                    if(url.Length-comma-1>(MaxBytes+2L)/3*4)throw new LivLivError("too-large","Poster exceeds the encoded byte limit");
                    bytes=Convert.FromBase64String(url.Substring(comma+1));
                }
                else
                {
                    Uri uri;
                    if(baseUri!=null&&Uri.TryCreate(baseUri,url,out var relative))uri=relative;
                    else if(!Uri.TryCreate(url,UriKind.Absolute,out uri))throw new ArgumentException("Relative poster URL needs a base URL");
                    if(uri.Scheme!="http"&&uri.Scheme!="https"&&uri.Scheme!="file")throw new ArgumentException("Unsupported poster URL scheme");
                    bytes=(await transport.Get(uri,MaxBytes,false,request.Token)).bytes;
                }
                request.Token.ThrowIfCancellationRequested();
                if(bytes==null||bytes.Length>MaxBytes)throw new LivLivError("too-large","Poster exceeds the encoded byte limit");
                fresh=new TextureDecoder().Decode(bytes);
                // TextureDecoder uses the renderer's premultiplied contract; standard UGUI needs straight alpha.
                var pixels=fresh.GetPixels32();
                for(int i=0;i<pixels.Length;i++)
                {
                    var c=pixels[i];
                    if(c.a==0)c.r=c.g=c.b=0;
                    else{c.r=Straight(c.r,c.a);c.g=Straight(c.g,c.a);c.b=Straight(c.b,c.a);}
                    pixels[i]=c;
                }
                fresh.SetPixels32(pixels);fresh.Apply(true,false);fresh.name="LivLiv poster";
                if(mine!=generation||request.IsCancellationRequested||!isActiveAndEnabled)return;
                owned=fresh;fresh=null;image.texture=owned;image.enabled=true;
                image.rectTransform.pivot=new Vector2(.5f,.5f);
                fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=(float)owned.width/owned.height;
            }
            catch(OperationCanceledException){}
            catch(Exception error)
            {
                if(mine==generation&&!request.IsCancellationRequested)
                {
                    LastError=error.Message;var handlers=Failed;
                    if(handlers!=null)foreach(Action<string> handler in handlers.GetInvocationList())try{handler(LastError);}catch(Exception e){Debug.LogException(e);}
                }
            }
            finally{TextureDecoder.Destroy(fresh);if(ReferenceEquals(pending,request))pending=null;request.Dispose();}
        }
        private static byte Straight(byte channel,byte alpha)=>(byte)Math.Min(255,(channel*255+alpha/2)/alpha);
    }
}
