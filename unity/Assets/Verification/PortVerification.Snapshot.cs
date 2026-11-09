// QA-only entry point: exercise the public snapshot API without adding sample UI.
using System;
using System.Runtime.InteropServices;
using LivLiv.Unity;
using UnityEngine;
public sealed partial class PortVerification
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void LivLivProbe_Snapshot(string json);
    [Serializable] private sealed class SnapshotEvidence
    {
        public string png,raw,error;
        public double before,after,scale;
        public bool playing,visible,activeRestored;
        public int width,height;
    }
    [Serializable] private sealed class MaskEvidence
    {
        public string raw,error;
        public double scale;
        public int width,height,maskWidth,maskHeight,maskChannels;
    }
    // Direct renderer QA: explicit gaze/blink, bypassing the sample's paused driver composition.
    public void MaskFrame(string pose)
    {
        var report=new MaskEvidence();Texture2D raw=null;var previous=RenderTexture.active;
        bool playing=player.Playing;var core=player.Renderer.Core;double x=core.view.x;
        try
        {
            player.Pause();SetPose("neutral");
            var p=core.@params;p.angleX=pose=="combined"?8:0;p.angleY=pose=="combined"?-3:0;
            p.eyeBallX=pose=="glance"||pose=="combined"||pose=="offscreen"?.8:0;
            p.eyeBallY=pose=="combined"?.3:0;p.eyeLOpen=p.eyeROpen=pose=="halfblink"||pose=="combined"?.5:1;
            core.move.s=1;core.time=0;core.ResetPhysics();core.ResetMouth();
            for(int i=0;i<30;i++)core.Update(1d/60);
            if(pose=="offscreen")core.view.x=10;
            player.Renderer.Render(target,0);report.width=target.width;report.height=target.height;report.scale=core.move.s;
            foreach(var layer in core.layers)if(((string)layer.def["role"]??"").StartsWith("eye_",StringComparison.Ordinal)&&layer.maskCh>=0)report.maskChannels++;
            foreach(var rt in Resources.FindObjectsOfTypeAll<RenderTexture>())if(rt.name=="LivLiv eye masks"){report.maskWidth=rt.width;report.maskHeight=rt.height;}
            RenderTexture.active=target;raw=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
            raw.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);report.raw=Convert.ToBase64String(raw.EncodeToPNG());
        }
        catch(Exception e){report.error=e.ToString();}
        finally
        {
            RenderTexture.active=previous;TextureDecoder.Destroy(raw);core.view.x=x;
            player.@params.eyeBallX=player.@params.eyeBallY=0;SetPose("neutral");player.@params.angleX=player.@params.angleY=0;
            if(playing)player.Play();LivLivProbe_Snapshot(JsonUtility.ToJson(report));
        }
    }
    public void SnapshotFrame(string pose)
    {
        var report=new SnapshotEvidence();Texture2D raw=null;var previous=RenderTexture.active;
        bool playing=player.Playing,visible=player.visible;
        try
        {
            SetPose(pose);player.@params.angleX=previewX;player.@params.angleY=previewY;
            player.Pause();player.visible=false;
            var core=player.Renderer.Core;core.time=0;core.ResetPhysics();core.ResetMouth();core.smileShown=0;
            report.before=core.time;report.png=player.Snapshot();report.after=core.time;
            report.scale=core.move.s;report.playing=player.Playing;report.visible=player.visible;
            report.activeRestored=RenderTexture.active==previous;report.width=target.width;report.height=target.height;
            RenderTexture.active=target;raw=new Texture2D(target.width,target.height,TextureFormat.RGBA32,false,true);
            raw.ReadPixels(new Rect(0,0,target.width,target.height),0,0,false);
            report.raw=Convert.ToBase64String(raw.EncodeToPNG());
        }
        catch(Exception e){report.error=e.ToString();}
        finally
        {
            RenderTexture.active=previous;TextureDecoder.Destroy(raw);player.visible=visible;if(playing)player.Play();
            LivLivProbe_Snapshot(JsonUtility.ToJson(report));
        }
    }
#endif
}
