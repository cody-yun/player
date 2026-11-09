// Mapping of puppet.ts render()/drawMouth()/drawNewBrows()/drawMasks().
using System;
using System.Collections.Generic;
using System.Linq;
using LivLiv.Runtime;
using LivLiv.Platform;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivLiv.Unity
{
    public sealed partial class PuppetRenderer
    {
        private void DrawLayer(CommandBuffer cmd,Vector4 view,Layer layer,Texture2D texture,double alpha=1,int pass=0,int reference=0,float cut=0,Mesh mesh=null,RenderTexture target=null)
        {
            var p=new MaterialPropertyBlock();p.SetTexture("_MainTex",texture);p.SetVector("_View",view);p.SetFloat("_Alpha",(float)alpha);p.SetFloat("_Cut",cut);
            if(pass==7)
            {
                p.SetTexture("_MaskTex",eyeMasks);p.SetVector("_MaskRect",new Vector4(eyeMaskOffset.x,eyeMaskOffset.y,eyeMasks.width,eyeMasks.height));
                var channel=new Vector4();channel[layer.geometry.maskCh]=1;p.SetVector("_MaskSel",channel);p.SetFloat("_Soft",(float)layer.geometry.soft);
            }
            var mat=material;
            if(reference!=0&&!stencilMaterials.TryGetValue(reference,out mat)){mat=new Material(material){hideFlags=HideFlags.HideAndDontSave};mat.SetInt("_StencilRef",reference);stencilMaterials.Add(reference,mat);}
            cmd.DrawMesh(mesh??layer.mesh,Matrix4x4.identity,mat,0,pass,p);
        }
        private Vector2 eyeMaskOffset;
        private void DrawMasks(CommandBuffer cmd,RenderTexture target,Vector4 view,double[] sourceView,float flip,Dictionary<string,Layer> byId)
        {
            foreach(var l in layers){l.geometry.maskCh=-1;l.geometry.stencilRef=0;}
            if(!SoftIris)return;
            var eyes=new List<Layer>(4);
            foreach(var l in layers)
            {
                if(!((string)l.def["role"]??"").StartsWith("iris_",StringComparison.Ordinal)||l.geometry.soft<=0||eyes.Count>=4)continue;
                if(l.def["clip"]?.Type!=JTokenType.String||!byId.TryGetValue((string)l.def["clip"],out var parent)||parent.mask==null||parent.geometry.maskCh>=0)continue;
                if(Core.hidden.Contains((string)l.def["id"])||Core.hidden.Contains((string)parent.def["id"]))continue;
                parent.geometry.maskCh=eyes.Count;eyes.Add(parent);
            }
            if(eyes.Count==0)return;
            var bounds=MaskBounds.Calculate(eyes.Select(e=>e.geometry.output),target.width,target.height,sourceView[0],sourceView[1],sourceView[2],sourceView[3]);
            if(!bounds.Renderable){foreach(var eye in eyes)eye.geometry.maskCh=-1;return;}
            int width=MaskBounds.Grow(eyeMasks==null?0:eyeMasks.width,bounds.width),height=MaskBounds.Grow(eyeMasks==null?0:eyeMasks.height,bounds.height);
            if(width>SystemInfo.maxTextureSize||height>SystemInfo.maxTextureSize)throw new LivLivError("too-large","Eye mask exceeds GPU texture size");
            if(eyeMasks==null||eyeMasks.width!=width||eyeMasks.height!=height||!eyeMasks.IsCreated())
            {
                var next=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="LivLiv eye masks",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
                if(!next.Create()){TextureDecoder.Destroy(next);throw new LivLivError("missing-feature","Eye mask allocation failed");}
                if(eyeMasks!=null){eyeMasks.Release();TextureDecoder.Destroy(eyeMasks);}eyeMasks=next;
            }
            // Equivalent to upstream viewport(-x0,-y0,w,h), without negative viewports on Metal/D3D.
            // The existing GPU projection flip already aligns fragment/texture pixels with source bounds.
            float rx=(float)target.width/width,ry=(float)target.height/height;
            var cropped=new Vector4(view.x*rx,view.y*ry,(view.z+1)*rx-1-2f*bounds.x/width,view.w*ry+flip*(ry-1-2f*bounds.y/height));
            eyeMaskOffset=new Vector2(bounds.x,bounds.y);
            cmd.SetRenderTarget(eyeMasks);cmd.SetViewport(new Rect(0,0,width,height));cmd.ClearRenderTarget(false,true,Color.clear);
            for(int i=0;i<eyes.Count;i++)DrawLayer(cmd,cropped,eyes[i],eyes[i].mask,pass:3+i);
        }
        private void DrawAnimated(CommandBuffer cmd,RenderTexture target,Vector4 view,Dictionary<string,Layer> byId)
        {
            var core=Core;var pose=core.@params;
            void Draw(Layer l,Texture2D tex,double alpha=1,Mesh mesh=null)=>DrawLayer(cmd,view,l,tex,alpha,mesh:mesh);
            void Mouth(Layer l)
            {
                int f=core.mouthFrame;double m=Math.Min(1,Math.Max(0,pose.mouthOpen));double py=l.def["pivot"] is JArray pivot?(double)pivot[1]:(double)l.def["y"];
                double k=core.frameKind[f]=="half"?.88+.12*core.FrameEase(f,m):.9+.1*core.FrameEase(f,m);
                Mesh mesh=null;
                if(f>0&&(!core.vis.TryGetValue("shut",out var shut)||f!=shut))
                {
                    var geometry=l.geometry;mesh=Alternate(l,ref l.mouthMesh);
                    for(int i=0;i<l.vertices.Length;i++)l.vertices[i]=new Vector3(geometry.output[2*i],(float)(geometry.output[2*i+1]+(geometry.rest[2*i+1]-py)*(k-1)*(core.chinFollow?1d-geometry.wJaw[i]:1)));
                    mesh.vertices=l.vertices;mesh.RecalculateBounds();
                }
                Draw(l,l.frames[f],mesh:mesh);
            }
            int lastExpr=core.talkLeft>0?layers.FindLastIndex(l=>(string)l.def["role"]=="expr"):-1;
            int lastBrow=layers.FindLastIndex(l=>(string)l.def["id"]=="brows"||(string)l.def["role"]=="expr_brows");bool newBrows=lastBrow<0;Layer deferred=null;
            void NewBrows()
            {
                newBrows=true;if(core.headHidden)return;
                foreach(var e in layers)
                {
                    if(e.brows==null||(string)e.def["role"]!="expr"||core.hidden.Contains((string)e.def["id"])||Only!=null&&!Only(e.def))continue;
                    double alpha=core.ExprAlpha(e.def);if(alpha<=.01)continue;Mesh mesh=null;
                    if(e.geometry.outB!=null){mesh=Alternate(e,ref e.browMesh);Upload(e,mesh,e.geometry.outB);}Draw(e,e.brows,alpha,mesh);
                }
            }
            int reference=0;
            for(int li=0;li<layers.Count;li++)
            {
                var l=layers[li];var d=l.def;string role=(string)d["role"]??"";
                if(deferred!=null&&li>lastExpr){Mouth(deferred);deferred=null;}
                if(!newBrows&&li>lastBrow)NewBrows();
                if(core.hidden.Contains((string)d["id"])||((string)d["group"]=="body"?core.bodyHidden:core.headHidden)||Only!=null&&!Only(d))continue;
                if((role=="hole"||role=="edge"||role=="edge_wide")&&!Masks)continue;
                if(role=="mouth"&&l.frames.Count>=3){if(li<lastExpr)deferred=l;else Mouth(l);continue;}
                if(role=="expr"){double alpha=core.ExprAlpha(d);if(alpha>.01)Draw(l,core.talkLeft>0&&l.talk!=null?l.talk:l.main,alpha);continue;}
                if(role=="expr_brows"){double alpha=core.ExprAlpha(d);if(alpha>.01)Draw(l,l.main,alpha);continue;}
                if((string)d["id"]=="brows")
                {
                    double fade=0;foreach(var e in layers)if((string)e.def["role"]=="expr"&&(string)e.def["brows"]=="redrawn"&&!core.hidden.Contains((string)e.def["id"]))fade=Math.Max(fade,core.ExprAlpha(e.def));
                    if(fade<.99)Draw(l,l.main,1-fade);continue;
                }
                if(role.StartsWith("lid_",StringComparison.Ordinal)){double alpha=1-Params.Smooth(.05,.35,role=="lid_L"?pose.eyeLOpen:pose.eyeROpen);if(alpha>.01)Draw(l,l.main,alpha);continue;}
                if(role.StartsWith("eye_",StringComparison.Ordinal))
                {
                    Draw(l,l.main,Params.Smooth(.02,.2,role=="eye_L"?pose.eyeLOpen:pose.eyeROpen));l.geometry.stencilRef=0;
                    if(l.mask!=null&&l.geometry.maskCh<0){reference=reference%250+1;l.geometry.stencilRef=reference;DrawLayer(cmd,view,l,l.mask,pass:1,reference:reference,cut:.5f);}continue;
                }
                if(role=="blush"){double alpha=0;if(d["blush"] is JObject blush)foreach(var weight in blush.Properties())alpha+=(double)weight.Value*core.ExprValue(weight.Name);if(alpha>.01)Draw(l,l.main,Math.Min(1,alpha));continue;}
                if(role.StartsWith("iris_",StringComparison.Ordinal)||role.StartsWith("shine_",StringComparison.Ordinal))
                {
                    if(d["clip"]?.Type!=JTokenType.String||!byId.TryGetValue((string)d["clip"],out var parent))continue;
                    double alpha=Params.Smooth(.02,.2,role.EndsWith("_L",StringComparison.Ordinal)?pose.eyeLOpen:pose.eyeROpen);
                    if(parent.geometry.maskCh>=0){l.geometry.maskCh=parent.geometry.maskCh;DrawLayer(cmd,view,l,l.main,alpha,7,target:target);}
                    else if(parent.geometry.stencilRef!=0)DrawLayer(cmd,view,l,l.main,alpha,2,parent.geometry.stencilRef);
                    continue;
                }
                double mood=core.MoodAlpha(d);if(mood>.01)Draw(l,l.main,mood);
            }
            if(deferred!=null)Mouth(deferred);if(!newBrows)NewBrows();
        }
    }
}
