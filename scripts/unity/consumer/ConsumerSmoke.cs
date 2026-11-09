// Copied into a fresh consumer project. Uses public package APIs only.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity;
using LivLiv.Unity.UI;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using PackageInfo=UnityEditor.PackageManager.PackageInfo;
public sealed class ConsumerSmoke
{
    [UnityTest] public IEnumerator InstalledPackagesRenderAndDispose()
    {
        if(!Player.IsSupported())throw new Exception("Installed Player capability probe failed");
        var root=new GameObject("Consumer canvas",typeof(RectTransform),typeof(Canvas));
        root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
        var child=new GameObject("Consumer character",typeof(RectTransform),typeof(RawImage));child.transform.SetParent(root.transform,false);
        ((RectTransform)child.transform).sizeDelta=new Vector2(320,480);
        var surface=child.AddComponent<PlayerSurface>();
        var player=surface.Initialize(new PlayerOptions{idle=false,autoplay=false});
        string output=Path.GetFullPath(Path.Combine(Application.dataPath,"../Evidence"));Directory.CreateDirectory(output);
        Texture2D read=null;RenderTexture owned=null;Material ownedMaterial=null;Evidence evidence=null;
        try
        {
            yield return null;
            player.Load(ModelPackage.FromZip(File.ReadAllBytes(Path.Combine(Application.dataPath,"ConsumerModel.bytes"))));
            ModelExpression[] expressions=player.Expressions;
            if(!ReferenceEquals(expressions,player.Info.expressions)||expressions.Length==0)throw new Exception("Installed Player expressions lost model metadata");
            string label=expressions[0].label;Newtonsoft.Json.Linq.JObject translations=expressions[0].labels;
            if(string.IsNullOrEmpty(label)||(string)translations["en"]!="Smile")throw new Exception("Installed expression label types/translations were not preserved");
            Canvas.ForceUpdateCanvases();surface.Refresh();
            owned=surface.Target;ownedMaterial=child.GetComponent<RawImage>().material;
            var png=player.SnapshotPng();File.WriteAllBytes(Path.Combine(output,"consumer.png"),png);
            var picked=player.LoadAsync(new Dictionary<string,byte[]>{{"avatar.data",File.ReadAllBytes(Path.Combine(Application.dataPath,"ConsumerModel.bytes"))}});
            while(!picked.IsCompleted)yield return null;
            if(picked.IsFaulted||picked.IsCanceled)throw new Exception("Installed Player did not accept a picked archive",picked.Exception);
            if(!System.Linq.Enumerable.SequenceEqual(png,player.SnapshotPng()))throw new Exception("Picked archive changed rendered pixels");
            read=new Texture2D(2,2,TextureFormat.RGBA32,false,true);if(!read.LoadImage(png))throw new Exception("Snapshot decode failed");
            int active=0;foreach(var pixel in read.GetPixels32())if(pixel.a>0)active++;
            if(active<1000||player.Renderer.Core.time!=0)throw new Exception("Consumer render failed");
            var core=PackageInfo.FindForAssembly(typeof(Player).Assembly);var ui=PackageInfo.FindForAssembly(typeof(PlayerSurface).Assembly);
            string nativePath=null;
#if LIVLIV_CONSUMER_NATIVE
            var native=PackageInfo.FindForAssembly(typeof(LivLiv.Unity.MediaPipe.NativeFaceInput).Assembly);
            if(native.source!=PackageSource.LocalTarball)throw new Exception("Native adapter did not load tarball");
            nativePath=native.resolvedPath;
#endif
            if(core.source!=PackageSource.LocalTarball||ui.source!=PackageSource.LocalTarball)throw new Exception("Consumer did not load tarballs");
            if(!core.resolvedPath.Contains("Library/PackageCache/")||!ui.resolvedPath.Contains("Library/PackageCache/"))throw new Exception("Unexpected package origin");
            evidence=new Evidence{result="Passed",unity=Application.unityVersion,
                graphics=SystemInfo.graphicsDeviceType.ToString(),core=core.resolvedPath,ui=ui.resolvedPath,native=nativePath,active=active,width=read.width,height=read.height,expressionLabels=true,pickedArchive=true};
            player.NotifyContextLost();
            if(!player.ContextLost)throw new Exception("Installed Player did not preserve context loss");
            try{player.Load((ModelPackage)null);throw new Exception("Lost Player accepted a synchronous load");}
            catch(LivLivError error){if(error.code!="context-lost")throw;}
            var rejected=player.LoadAsync((ModelPackage)null);
            if(!rejected.IsFaulted||!(rejected.Exception.GetBaseException() is LivLivError lost)||lost.code!="context-lost")throw new Exception("Lost Player did not reject async load");
            evidence.contextLostHandled=true;
        }
        finally{TextureDecoder.Destroy(read);UnityEngine.Object.Destroy(root);}
        yield return null;yield return null;
        if(!player.IsDisposed||owned!=null||ownedMaterial!=null)throw new Exception("Destroy did not dispose owned player/target/material");
        var released=new RenderTexture(16,16,24);Player manual=null;
        try
        {
            if(!released.Create())throw new Exception("Consumer release target creation failed");
            manual=new Player(released,new PlayerOptions{autoplay=false,idle=false});manual.Dispose(new PlayerDisposeOptions{release=true});
            yield return null;yield return null;
            if(released.IsCreated())throw new Exception("Installed explicit release did not return GPU storage");
            try{using(var unexpected=new Player(released)){}throw new Exception("Installed player reused a released target");}
            catch(LivLivError error){if(error.code!="released")throw;}
            evidence.deferredRelease=true;
        }
        finally{manual?.Dispose();released.Release();UnityEngine.Object.Destroy(released);}
        yield return null;yield return null;
        evidence.disposed=true;evidence.supported=true;File.WriteAllText(Path.Combine(output,"result.json"),JsonUtility.ToJson(evidence,true));
    }
    [Serializable]private sealed class Evidence{public string result,unity,graphics,core,ui,native;public int active,width,height;public bool disposed,supported,contextLostHandled,expressionLabels,deferredRelease,pickedArchive;}
}
