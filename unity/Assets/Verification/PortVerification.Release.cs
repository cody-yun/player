// QA-only public-API smoke. No camera/microphone/network and no global context loss.
using System;
using System.IO;
using System.Threading.Tasks;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class ReleaseEvidence
    {
        public string result="Running",error,platform,graphics;
        public bool defaultRetained,immediateClaimCanceled,completedReleased,releasedRejected,recreatedRejected,otherRendered,mainAlive,targetObjectRetained;
        public bool sharedOwnersNotified,retargetedOwnerPreserved;
        public int mainStartFrame,mainEndFrame,otherActivePixels;
    }
    private void StartReleaseProbeIfRequested()
    {
#if !UNITY_WEBGL || UNITY_EDITOR
        var args=Environment.GetCommandLineArgs();for(int i=0;i+1<args.Length;i++)if(args[i]=="-livlivReleaseProbe"){ReleaseTargetProbe(args[i+1]);return;}
#endif
    }
    public async void ReleaseTargetProbe(string directory)
    {
        var r=new ReleaseEvidence{platform=Application.platform.ToString(),graphics=SystemInfo.graphicsDeviceType.ToString(),mainStartFrame=frames};
        RenderTexture t=null,other=null,shared=null;Player owner=null,survivor=null,aliasOwner=null,alias=null,moved=null;
        try
        {
            t=new RenderTexture(64,64,24);t.Create();other=new RenderTexture(64,64,24);other.Create();
            owner=new Player(t,new PlayerOptions{autoplay=false,idle=false});owner.Dispose();await Task.Yield();await Task.Yield();r.defaultRetained=t.IsCreated();
            owner=new Player(t,new PlayerOptions{autoplay=false,idle=false});owner.Dispose(new PlayerDisposeOptions{release=true});
            owner=new Player(t,new PlayerOptions{autoplay=false,idle=false});await Task.Yield();await Task.Yield();r.immediateClaimCanceled=t.IsCreated();
            survivor=new Player(other,new PlayerOptions{autoplay=false,idle=false});survivor.Load(ModelPackage.FromZip(Resources.Load<TextAsset>("LivLivVerification").bytes));
            owner.Dispose(new PlayerDisposeOptions{release=true});await Task.Yield();await Task.Yield();r.completedReleased=!t.IsCreated();r.targetObjectRetained=t!=null;
            bool Rejected()
            {try{using(var unexpected=new Player(t,new PlayerOptions{autoplay=false,idle=false})){}return false;}catch(LivLivError e){if(e.code!="released")throw;return true;}}
            r.releasedRejected=Rejected();t.Create();r.recreatedRejected=Rejected();
            shared=new RenderTexture(64,64,24);shared.Create();aliasOwner=new Player(shared);alias=new Player(shared);moved=new Player(shared);moved.Target=other;
            int losses=0;alias.OnContextLost+=()=>losses++;aliasOwner.Dispose(new PlayerDisposeOptions{release=true});
            await Task.Yield();await Task.Yield();
            r.sharedOwnersNotified=alias.ContextLost&&losses==1&&!shared.IsCreated();r.retargetedOwnerPreserved=!moved.ContextLost&&moved.Target==other;
            survivor.Tick(1d/60);var png=survivor.SnapshotPng();
            var image=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            try
            {
                if(!ImageConversion.LoadImage(image,png)||image.width!=64||image.height!=64)throw new Exception("Surviving player PNG decode failed");
                foreach(var pixel in image.GetPixels32())if(pixel.a>0)r.otherActivePixels++;
            }
            finally{Destroy(image);}
            r.otherRendered=other.IsCreated()&&r.otherActivePixels>50&&!survivor.ContextLost;
            await Task.Yield();await Task.Yield();r.mainEndFrame=frames;r.mainAlive=frames>r.mainStartFrame&&!player.IsDisposed&&!player.ContextLost;
            if(!r.defaultRetained||!r.immediateClaimCanceled||!r.completedReleased||!r.releasedRejected||!r.recreatedRejected||!r.otherRendered||!r.mainAlive||!r.targetObjectRetained||!r.sharedOwnersNotified||!r.retargetedOwnerPreserved)
                throw new Exception("Render target release lifecycle mismatch");
            r.result="Passed";
        }
        catch(Exception exception){r.result="Failed";r.error=exception.ToString();}
        finally
        {
            owner?.Dispose();survivor?.Dispose();aliasOwner?.Dispose();alias?.Dispose();moved?.Dispose();foreach(var target in new[]{t,other,shared})if(target!=null){target.Release();Destroy(target);}
#if UNITY_WEBGL && !UNITY_EDITOR
            LivLivProbe_Snapshot(JsonUtility.ToJson(r));
#else
            Directory.CreateDirectory(directory);File.WriteAllText(Path.Combine(directory,"release.json"),JsonUtility.ToJson(r,true));
            Application.Quit(r.result=="Passed"?0:1);
#endif
        }
    }
}
