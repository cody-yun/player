#if UNITY_WEBGL && !UNITY_EDITOR
// QA-only loopback loading probe. Public API and the real sample UGUI presenter; no device capture.
using System;
using System.Runtime.InteropServices;
using System.Threading;
using LivLiv;
using LivLiv.Platform;
using UnityEngine;
public sealed partial class PortVerification
{
    [DllImport("__Internal")] private static extern void LivLivProbe_Poster(string json);
    [DllImport("__Internal")] private static extern void LivLivProbe_PosterLoad(string json);
    [Serializable] private sealed class PosterRequest { public string id,url,poster; }
    [Serializable] private sealed class PosterResult { public string id,state,error; }
    [Serializable] private sealed class PosterState
    {
        public string url,error;public bool showing,loading,ready;public double scale;
        public float width,height,boundsWidth,boundsHeight;public int textures;
        public int textureWidth,textureHeight,topRed,topBlue,bottomRed,bottomBlue;
    }
    private bool posterProbe;
    private CancellationTokenSource posterModelLoad;
    public void ResetPosterProbe()
    {
        posterProbe=true;posterModelLoad?.Cancel();player.Dispose();
        var previous=RenderTexture.active;
        try{RenderTexture.active=target;GL.Clear(true,true,Color.clear);}finally{RenderTexture.active=previous;}
        player=new LivLiv.Unity.Player(target,new PlayerOptions{idle=false});player.Use(new SamplePresentation());
        BindPlayerLoss();
        GetComponent<VerificationHud>().BindPoster(player);ResetPreview();UpdatePosterProbe();
    }
    public async void LoadPosterModel(string json)
    {
        var args=JsonUtility.FromJson<PosterRequest>(json);var request=new CancellationTokenSource();posterModelLoad=request;
        var result=new PosterResult{id=args.id,state="loading"};LivLivProbe_PosterLoad(JsonUtility.ToJson(result));
        try{await player.LoadAsync(args.url,new LoadOptions{poster=args.poster,cancellation=request.Token});result.state="ready";}
        catch(OperationCanceledException){result.state="cancelled";}
        catch(Exception e){result.state="failed";result.error=e.Message;}
        finally{if(ReferenceEquals(posterModelLoad,request))posterModelLoad=null;request.Dispose();LivLivProbe_PosterLoad(JsonUtility.ToJson(result));UpdatePosterProbe();}
    }
    public void CancelPosterModel()=>posterModelLoad?.Cancel();
    public void EndPosterProbe()
    {
        ResetPosterProbe();player.Load(ModelPackage.FromZip(Resources.Load<TextAsset>("LivLivVerification").bytes));
        ResetPreview();UpdatePosterProbe();posterProbe=false;
    }
    private void UpdatePosterProbe()
    {
        if(!posterProbe)return;var presenter=GetComponent<VerificationHud>().Poster;var rect=(RectTransform)presenter.transform;
        var parent=(RectTransform)rect.parent;int textures=0;
        foreach(var t in Resources.FindObjectsOfTypeAll<Texture2D>())if(t.name=="LivLiv poster")textures++;
        var state=new PosterState{url=player.Poster,error=presenter.LastError,showing=presenter.Showing,
            loading=presenter.Loading,ready=player.Info!=null,scale=player.Renderer.Core.move.s,
            width=rect.rect.width,height=rect.rect.height,boundsWidth=parent.rect.width,boundsHeight=parent.rect.height,textures=textures};
        var texture=presenter.GetComponent<UnityEngine.UI.RawImage>().texture as Texture2D;
        if(presenter.Showing&&texture!=null)
        {
            state.textureWidth=texture.width;state.textureHeight=texture.height;
            var pixels=texture.GetPixels32();var bottom=pixels[(texture.height/6)*texture.width+texture.width/2];
            var top=pixels[(texture.height*5/6)*texture.width+texture.width/2];
            state.topRed=top.r;state.topBlue=top.b;state.bottomRed=bottom.r;state.bottomBlue=bottom.b;
        }
        LivLivProbe_Poster(JsonUtility.ToJson(state));
    }
}
#endif
