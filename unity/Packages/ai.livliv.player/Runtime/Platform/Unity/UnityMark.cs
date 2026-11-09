// Unity GPU lifetime boundary for src/mark.ts. Canvas command generation lives in Mark/Brand.
using System;
using LivLiv.Platform;
using UnityEngine;
using UnityEngine.Rendering;

namespace LivLiv.Unity
{
    public sealed class UnityMark : IDisposable
    {
        private readonly Mark mark=new Mark();
        private readonly IMarkTextRasterizer textRasterizer;
        public UnityMark(IMarkTextRasterizer textRasterizer=null){this.textRasterizer=textRasterizer??new UnityMarkText();}
        private string drawn;
        private Texture2D texture;
        private Mesh mesh;
        private Material material;
        private bool disposed;
        public void Set(string kind,string text,bool center=true){if(disposed)throw new ObjectDisposedException(nameof(UnityMark));if(kind=="text"&&!string.IsNullOrEmpty(text)&&textRasterizer is IMarkTextValidator validator)validator.Validate(text);mark.Set(kind,text,center);}
        public void Prepare(int width,int height,double now,double dpr=1)
        {
            if(disposed)throw new ObjectDisposedException(nameof(UnityMark));
            if(!mark.Visible)return;
            var key=mark.CacheKey(width,height,dpr,now);if(key==drawn)return;
            var pixels=BrandRasterizer.Draw(width,height,mark.Draw(width,height,now,dpr),textRasterizer);
            // Raster output uses canvas top-down rows; Unity texture UV origin is bottom-left.
            int stride=width*4;var row=new byte[stride];
            for(int y=0;y<height/2;y++){int a=y*stride,b=(height-y-1)*stride;Buffer.BlockCopy(pixels,a,row,0,stride);Buffer.BlockCopy(pixels,b,pixels,a,stride);Buffer.BlockCopy(row,0,pixels,b,stride);}
            Texture2D fresh=null;
            try
            {
                fresh=new Texture2D(width,height,TextureFormat.RGBA32,false,true){name="LivLiv watermark",hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
                fresh.LoadRawTextureData(pixels);fresh.Apply(false,true);
                if(material==null)
                {
                    var shader=Resources.Load<Shader>("LivLivLayer");if(shader==null||!shader.isSupported)throw new LivLivError("missing-feature","Watermark shader unavailable");
                    material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
                }
                if(mesh==null)
                {
                    mesh=new Mesh{name="LivLiv watermark quad",hideFlags=HideFlags.HideAndDontSave};
                    mesh.vertices=new[]{new Vector3(0,0),new Vector3(1,0),new Vector3(0,1),new Vector3(1,1)};
                    mesh.uv=new[]{new Vector2(0,1),new Vector2(1,1),new Vector2(0,0),new Vector2(1,0)};mesh.triangles=new[]{0,1,2,1,3,2};mesh.RecalculateBounds();
                }
                TextureDecoder.Destroy(texture);texture=fresh;fresh=null;drawn=key;
            }
            finally {TextureDecoder.Destroy(fresh);}
        }
        public void Draw(CommandBuffer cmd)
        {
            if(disposed)throw new ObjectDisposedException(nameof(UnityMark));
            if(!mark.Visible)return;
            if(texture==null)throw new InvalidOperationException("Prepare the watermark first");
            var projection=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(0,1,1,0,-1,1),true);
            var p=new MaterialPropertyBlock();p.SetTexture("_MainTex",texture);p.SetVector("_View",new Vector4(projection.m00,projection.m11,projection.m03,projection.m13));p.SetFloat("_Alpha",1);p.SetFloat("_Cut",0);
            cmd.DrawMesh(mesh,Matrix4x4.identity,material,0,0,p);
        }
        public void Dispose(){if(disposed)return;disposed=true;TextureDecoder.Destroy(texture);TextureDecoder.Destroy(material);TextureDecoder.Destroy(mesh);texture=null;material=null;mesh=null;}
    }
}
