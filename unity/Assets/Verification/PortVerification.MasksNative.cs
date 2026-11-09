#if !UNITY_WEBGL || UNITY_EDITOR
// Native presentation probe: explicit gaze/blink and cropped-mask lifecycle on the built GPU backend.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Unity;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class NativeMaskStep
    {
        public string name;public int width,height,maskWidth,maskHeight,maskId,channels,active;
        public double scale;public long storageBytes,fullSizeBytes;
    }
    private static NativeMaskStep[] RunNativeMasks(ModelPackage package,string output)
    {
        var before=new HashSet<int>(Resources.FindObjectsOfTypeAll<RenderTexture>().Where(t=>t.name=="LivLiv eye masks").Select(t=>t.GetInstanceID()));
        var steps=new List<NativeMaskStep>();RenderTexture target=null,previousMask=null;
        using(var renderer=new PuppetRenderer())try
        {
            renderer.Load(package);
            foreach(string name in new[]{"glance","halfblink","combined","reused","larger","smaller","offscreen","recovered"})
            {
                int width=name=="larger"?1024:512,height=name=="larger"?1536:768;
                if(target==null||target.width!=width||target.height!=height)
                {
                    if(target!=null){target.Release();Destroy(target);}
                    target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);RequirePresentation(target.Create(),"Mask output target allocation");
                }
                var core=renderer.Core;var p=core.@params;
                bool combined=name!="glance"&&name!="halfblink";
                p.angleX=combined?8:0;p.angleY=combined?-3:0;p.eyeBallX=name=="halfblink"?0:.8;p.eyeBallY=combined?.3:0;
                p.eyeLOpen=p.eyeROpen=name=="glance"?1:.5;
                core.view.x=name=="offscreen"?10:0;core.move.s=1;core.time=0;core.ResetPhysics();core.ResetMouth();
                for(int i=0;i<30;i++)core.Update(1d/60);
                if(name=="recovered")previousMask.Release();
                renderer.Render(target,0);
                var mask=Resources.FindObjectsOfTypeAll<RenderTexture>().Single(t=>t.name=="LivLiv eye masks"&&t.IsCreated()&&!before.Contains(t.GetInstanceID()));
                var row=new NativeMaskStep{name=name,width=width,height=height,maskWidth=mask.width,maskHeight=mask.height,maskId=mask.GetInstanceID(),scale=core.move.s,
                    channels=core.layers.Count(l=>((string)l.def["role"]??"").StartsWith("eye_",StringComparison.Ordinal)&&l.maskCh>=0),storageBytes=(long)mask.width*mask.height*4,fullSizeBytes=(long)width*height*4};
                RequirePresentation(row.scale==1&&row.maskWidth%64==0&&row.maskHeight%64==0&&row.storageBytes<row.fullSizeBytes,"Mask size/scale mismatch: "+name);
                RequirePresentation(name=="offscreen"?row.channels==0:row.channels==2,"Mask channel mismatch: "+name);
                if(name=="reused"||name=="smaller"||name=="offscreen")RequirePresentation(mask==previousMask,"Mask storage not reused: "+name);
                if(name=="larger")RequirePresentation(mask!=previousMask&&mask.width>=previousMask.width&&mask.height>=previousMask.height,"Mask storage did not grow");
                if(name=="recovered")RequirePresentation(mask!=previousMask,"Lost mask storage not replaced");
                var active=RenderTexture.active;var image=new Texture2D(width,height,TextureFormat.RGBA32,false,true);
                try
                {
                    RenderTexture.active=target;image.ReadPixels(new Rect(0,0,width,height),0,0,false);row.active=image.GetPixels32().Count(c=>c.a>0);
                    File.WriteAllBytes(Path.Combine(output,"mask-"+name+".png"),image.EncodeToPNG());
                }
                finally{RenderTexture.active=active;Destroy(image);}
                if(name!="offscreen")RequirePresentation(row.active>10000,"Mask probe lost visible character");
                steps.Add(row);previousMask=mask;
            }
        }
        finally{if(target!=null){target.Release();Destroy(target);}}
        return steps.ToArray();
    }
}
#endif
