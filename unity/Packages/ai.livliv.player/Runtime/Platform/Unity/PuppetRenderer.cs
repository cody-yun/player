// Unity draw/resource adapter for puppet.ts; numeric deformation lives in Runtime.Puppet.
// All source images are loaded (including expression/mouth alternatives) before installation.
// RenderNeutral is the reproducible rest-pose path; Update + Render draws the animated core.
using System;
using System.Collections.Generic;
using System.Linq;
using LivLiv.Platform;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivLiv.Unity
{
    public sealed partial class PuppetRenderer : IDisposable
    {
        private sealed class Layer : IDisposable
        {
            public JObject def;
            public Mesh mesh,mouthMesh,browMesh;
            public Vector3[] vertices;
            public byte[] gridAlpha,lashRgba;
            public Texture2D main,mask,talk,brows;
            public LivLiv.Runtime.PuppetLayer geometry;
            public readonly List<Texture2D> frames=new List<Texture2D>();
            public void Dispose() { TextureDecoder.Destroy(mesh);TextureDecoder.Destroy(mouthMesh);TextureDecoder.Destroy(browMesh);TextureDecoder.Destroy(main);TextureDecoder.Destroy(mask);TextureDecoder.Destroy(talk);TextureDecoder.Destroy(brows);foreach(var f in frames)TextureDecoder.Destroy(f); }
        }
        private List<Layer> layers=new List<Layer>();
        private Material material;
        private readonly Dictionary<int,Material> stencilMaterials=new Dictionary<int,Material>();
        private bool disposed;
        private readonly IWebPDecoder webp;
        private UnityMark mark;
        private readonly IMarkTextRasterizer markText;
        public JObject Model { get; private set; }
        public LivLiv.Runtime.Puppet Core { get; private set; }=new LivLiv.Runtime.Puppet();
        public bool SoftIris=true,Masks;
        public Func<JObject,bool> Only;
        private RenderTexture eyeMasks;
        public long Texels { get; private set; }
        public PuppetRenderer(IWebPDecoder webp=null,IMarkTextRasterizer markText=null)
        {
            this.webp=webp;this.markText=markText??new UnityMarkText();mark=new UnityMark(this.markText);
            var shader=Resources.Load<Shader>("LivLivLayer");
            if (shader==null || !shader.isSupported) throw new LivLivError("missing-feature","LivLiv layer shader unavailable");
            material=new Material(shader) { hideFlags=HideFlags.HideAndDontSave };
        }
        public void Load(ModelPackage package)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PuppetRenderer));
            var fresh=new List<Layer>(); var decoder=new TextureDecoder(webp);
            var core=Core.ForReplacement();core.LoadDefinition(package.model);
            try
            {
                foreach(var def in ((JArray)package.model["layers"]).Cast<JObject>())
                {
                    var layer=new Layer { def=def };fresh.Add(layer);
                    Texture2D Image(string path)=>decoder.Decode(package.ReadImage(def,path));
                    layer.main=decoder.Decode(package.ReadImage(def,(string)def["texture"]), ((string)def["role"]??"").StartsWith("lid_",StringComparison.Ordinal)?(Action<Color32[],int,int,bool>)((pixels,w,h,isWebP)=>layer.lashRgba=LayerSampling.CanvasRgba(pixels,w,h,isWebP)):null);
                    foreach(var f in def["frames"] as JArray??new JArray())layer.frames.Add(Image((string)f));
                    if(def["mask"]?.Type==JTokenType.String)layer.mask=Image((string)def["mask"]);
                    if(def["talk"]?.Type==JTokenType.String)layer.talk=Image((string)def["talk"]);
                    if(def["brow_tex"]?.Type==JTokenType.String)layer.brows=Image((string)def["brow_tex"]);
                    var grid=new LivLiv.Runtime.LayerLattice(package.model,def);
                    layer.gridAlpha=LayerSampling.Grid(layer.main,layer.frames,grid);
                    layer.geometry=core.BuildLayer(def,layer.gridAlpha);
                    var geometry=layer.geometry;core.layers.Add(geometry);int count=geometry.rest.Length/2;
                    var vertices=layer.vertices=new Vector3[count];var uv=new Vector2[count];
                    for(int i=0;i<count;i++){vertices[i]=new Vector3(geometry.rest[2*i],geometry.rest[2*i+1]);uv[i]=new Vector2(geometry.uv[2*i],1-geometry.uv[2*i+1]);}
                    layer.mesh=new Mesh { name="LivLiv "+(string)def["id"],hideFlags=HideFlags.HideAndDontSave };
                    layer.mesh.MarkDynamic();
                    layer.mesh.indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16;
                    layer.mesh.vertices=vertices;layer.mesh.uv=uv;layer.mesh.triangles=geometry.tri;layer.mesh.RecalculateBounds();
                }
                PrepareSamples(core,fresh);
                if(package.keyforms!=null&&!core.PrepareKeyforms(package.keyforms))Debug.LogWarning("LivLiv: keyforms.json is not one this player reads; the character plays on the rig alone");
                core.Deform();
            }
            catch { foreach(var l in fresh)l.Dispose();throw; }
            // Transactional replacement: failed loads retain the previous model and GPU resources.
            var nextMark=new UnityMark(markText);var meta=package.model["meta"] as JObject;var text=(string)package.model["watermark"];
            var kind=meta!=null?(string)meta["watermark"]?["kind"]??"none":text=="LivLiv"?"livliv":!string.IsNullOrEmpty(text)?"text":"none";
            try { nextMark.Set(kind,text,meta?["watermark"]?["center"]?.Type!=JTokenType.Boolean||(bool)meta["watermark"]["center"]); }
            catch { nextMark.Dispose();foreach(var l in fresh)l.Dispose();throw; }
            var old=layers;layers=fresh;Core=core;Model=package.model;Texels=decoder.texels;
            mark.Dispose();mark=nextMark;
            if(eyeMasks!=null){eyeMasks.Release();TextureDecoder.Destroy(eyeMasks);eyeMasks=null;}
            foreach(var l in old)l.Dispose();
        }
        public void Update(double deltaSeconds)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PuppetRenderer));
            if(Core==null)throw new InvalidOperationException("No model loaded");
            Core.Update(deltaSeconds);
        }
        public void RenderNeutral(RenderTexture target,double now=0,double dpr=1)=>RenderFrame(target,now,dpr,true);
        public void Render(RenderTexture target,double now=0,double dpr=1)=>RenderFrame(target,now,dpr,false);
        private static void Upload(Layer layer,Mesh mesh,float[] points)
        {
            for(int i=0;i<layer.vertices.Length;i++)layer.vertices[i]=new Vector3(points[2*i],points[2*i+1]);
            mesh.vertices=layer.vertices;mesh.RecalculateBounds();
        }
        private static Mesh Alternate(Layer layer,ref Mesh mesh)
        {
            if(mesh==null){mesh=UnityEngine.Object.Instantiate(layer.mesh);mesh.hideFlags=HideFlags.HideAndDontSave;mesh.MarkDynamic();}
            return mesh;
        }
        private void RenderFrame(RenderTexture target,double now,double dpr,bool neutral)
        {
            if(disposed)throw new ObjectDisposedException(nameof(PuppetRenderer));
            if(Model==null)throw new InvalidOperationException("No model loaded");
            if(target==null||target.depth<24)throw new ArgumentException("Render target needs a 24-bit depth/stencil buffer",nameof(target));
            mark.Prepare(target.width,target.height,now,dpr);
            foreach(var layer in layers)Upload(layer,layer.mesh,neutral?layer.geometry.rest:layer.geometry.output);
            var byId=new Dictionary<string,Layer>(StringComparer.Ordinal);foreach(var l in layers)byId[(string)l.def["id"]]=l;
            var maskRefs=new Dictionary<Layer,int>();int nextRef=0;
            var sourceView=Core.CalculateView(target.width,target.height);
            // Source vertex shader flips y; Unity's render-target projection applies the API-specific flip.
            float flip=GL.GetGPUProjectionMatrix(Matrix4x4.identity,true).m11;
            var view=new Vector4((float)sourceView[0],-(float)sourceView[1]*flip,(float)sourceView[2],-(float)sourceView[3]*flip);
            using(var cmd=new CommandBuffer { name="LivLiv layers" })
            {
                if(!neutral)DrawMasks(cmd,target,view,sourceView,flip,byId);
                cmd.SetRenderTarget(target);cmd.SetViewport(new Rect(0,0,target.width,target.height));cmd.ClearRenderTarget(true,true,Color.clear);
                void Draw(Layer l,Texture2D texture,float alpha=1,int pass=0,int reference=0,float cut=0)
                {
                    var p=new MaterialPropertyBlock();p.SetTexture("_MainTex",texture);p.SetVector("_View",view);p.SetFloat("_Alpha",alpha);p.SetFloat("_Cut",cut);p.SetInt("_StencilRef",reference);
                    // Stencil is fixed render state: a MaterialPropertyBlock cannot override it.
                    var mat=material;
                    if(reference!=0 && !stencilMaterials.TryGetValue(reference,out mat))
                    { mat=new Material(material) { hideFlags=HideFlags.HideAndDontSave };mat.SetInt("_StencilRef",reference);stencilMaterials.Add(reference,mat); }
                    cmd.DrawMesh(l.mesh,Matrix4x4.identity,mat,0,pass,p);
                }
                if(!neutral)DrawAnimated(cmd,target,view,byId);
                else foreach(var l in layers)
                {
                    var d=l.def;var role=(string)d["role"]??"";
                    if(role=="hole"||role=="edge"||role=="edge_wide"||role=="expr"||role=="expr_brows"||role=="blush"||role.StartsWith("lid_",StringComparison.Ordinal))continue;
                    if(role=="mouth"&&l.frames.Count>=3){Draw(l,l.frames[0]);continue;}
                    if(role.StartsWith("eye_",StringComparison.Ordinal))
                    {
                        Draw(l,l.main);
                        if(l.mask!=null){nextRef=nextRef%250+1;maskRefs[l]=nextRef;Draw(l,l.mask,1,1,nextRef,.5f);}continue;
                    }
                    if(role.StartsWith("iris_",StringComparison.Ordinal)||role.StartsWith("shine_",StringComparison.Ordinal))
                    {
                        if(d["clip"]?.Type==JTokenType.String && byId.TryGetValue((string)d["clip"],out var parent) && maskRefs.TryGetValue(parent,out var reference))Draw(l,l.main,1,2,reference);
                        continue;
                    }
                    // At neutral all expression weights are zero; show/hide retain the source semantics.
                    if(d["show"] is JObject)continue;
                    Draw(l,l.main);
                }
                mark.Draw(cmd);
                bool oldWrite=GL.sRGBWrite;
                try { GL.sRGBWrite=false;Graphics.ExecuteCommandBuffer(cmd); }
                finally { GL.sRGBWrite=oldWrite; }
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            mark.Dispose();
            foreach(var l in layers)l.Dispose();layers.Clear();TextureDecoder.Destroy(material);material=null;Model=null;Core=null;
            if(eyeMasks!=null){eyeMasks.Release();TextureDecoder.Destroy(eyeMasks);eyeMasks=null;}
            foreach(var m in stencilMaterials.Values)TextureDecoder.Destroy(m);stencilMaterials.Clear();
        }
    }
}
