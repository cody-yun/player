// QA-only package text marks, public Player/Snapshot API, and retained-resource counts.
using System;
using System.Collections;
using System.Linq;
using LivLiv;
using LivLiv.Platform;
using LivLiv.Unity;
using Newtonsoft.Json.Linq;
using UnityEngine;
public sealed partial class PortVerification
{
    [Serializable]private sealed class TextMarkEvidence
    {
        public string name,text,font,png,error,code;public int width,height,watermarks,coverageTargets,coverageMeshes,coverageMaterials;
    }
    public void TextMarkFrame(string name){StartCoroutine(TextMarkProbe(name));}
    private IEnumerator TextMarkProbe(string name,Action<TextMarkEvidence> completed=null)
    {
        var before=Resources.FindObjectsOfTypeAll<Texture2D>().Where(t=>t.name=="LivLiv watermark").Select(t=>t.GetInstanceID()).ToArray();
        var report=new TextMarkEvidence{name=name};
        try{RenderTextMark(report);}catch(Exception e){report.error=e.ToString();report.code=(e as LivLivError)?.code;}
        // The normal sample logo can blink/reallocate while we yield. Track only this synchronous probe's new textures.
        var owned=Resources.FindObjectsOfTypeAll<Texture2D>().Where(t=>t.name=="LivLiv watermark"&&!before.Contains(t.GetInstanceID())).ToArray();
        yield return null;yield return null;
        report.watermarks=owned.Count(t=>t!=null);
        report.coverageTargets=Resources.FindObjectsOfTypeAll<RenderTexture>().Count(t=>t.name=="LivLiv text coverage");
        report.coverageMeshes=Resources.FindObjectsOfTypeAll<Mesh>().Count(t=>t.name=="LivLiv text coverage");
        report.coverageMaterials=Resources.FindObjectsOfTypeAll<Material>().Count(t=>t.name=="LivLiv text coverage");
        if(completed!=null)completed(report);
#if UNITY_WEBGL && !UNITY_EDITOR
        else LivLivProbe_Snapshot(JsonUtility.ToJson(report));
#endif
    }
    private void RenderTextMark(TextMarkEvidence report)
    {
        report.text=report.name=="empty"||report.name=="retina-empty"?"":report.name=="default"?"기본 글꼴 Owner":report.name=="korean"||report.name=="retina"?"소유자 이름":report.name=="literal"?"<b>Owner</b>":report.name=="budget"?new string('A',8193):"Owner AV gy";
        int density=report.name=="retina"||report.name=="retina-empty"?2:1;report.width=512*density;report.height=768*density;
        var package=ModelPackage.FromZip(Resources.Load<TextAsset>("LivLivVerification").bytes);
        package.model["watermark"]=report.text;package.model["meta"]=new JObject{["watermark"]=new JObject{["kind"]="text",["text"]=report.text}};
        var target=new RenderTexture(report.width,report.height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);target.Create();
        try
        {
            var font=GetComponent<VerificationHud>().MarkFont;report.font=(report.name=="default"?Resources.Load<Font>("LivLivMarkFont"):font).name;
            using(var owner=new Player(target,new PlayerOptions{idle=false,autoplay=false},markText:report.name=="default"?null:new UnityMarkText(font)))
            {owner.Width=512;owner.Height=768;owner.Load(package);report.png=Convert.ToBase64String(owner.SnapshotPng());}
        }
        finally{target.Release();Destroy(target);}
        if(report.name=="budget")throw new Exception("Oversized text was not rejected");
    }
}
