// Unity Font/TextGenerator boundary for brand.ts drawMarkText. Main thread, borrowed font, no UGUI dependency.
using System;
using LivLiv.Platform;
using LivLiv.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
namespace LivLiv.Unity
{
    public sealed class UnityMarkText:IMarkTextRasterizer,IMarkTextValidator
    {
        private readonly Font font;
        private readonly FontStyle style;
        public UnityMarkText(Font font=null,FontStyle style=FontStyle.Bold)
        {this.font=font!=null?font:Resources.Load<Font>("LivLivMarkFont");this.style=style;}
        private static string Normalize(string text)=>(text??"").Replace('\t',' ').Replace('\n',' ').Replace('\r',' ').Replace('\f',' ');
        public void Validate(string text)
        {
            text=Normalize(text);if(text.Length>8192)throw new LivLivError("too-large","Owner mark text exceeds its budget");
            CheckFont(text,12);
        }
        private void CheckFont(string text,int size)
        {
            if(font==null||!font.dynamic)throw new LivLivError("missing-feature","Owner mark needs a dynamic Font with included font data");
            // Legacy Font APIs can return a successful but blank .notdef entry. Query the actual font face.
            if(FontEngine.LoadFontFace(font,size)!=FontEngineError.Success)throw new LivLivError("missing-feature","Owner mark font data cannot be loaded");
            for(int i=0;i<text.Length;i++)
            {
                int at=i;uint scalar=text[i];
                if(char.IsHighSurrogate(text[i])&&i+1<text.Length&&char.IsLowSurrogate(text[i+1])){scalar=(uint)char.ConvertToUtf32(text[i],text[i+1]);i++;}
                if(char.IsWhiteSpace(text,at)||char.GetUnicodeCategory(text,at)==System.Globalization.UnicodeCategory.Format)continue;
                if(!FontEngine.TryGetGlyphWithUnicodeValue(scalar,GlyphLoadFlags.LOAD_DEFAULT,out _))throw new LivLivError("missing-feature","Owner mark font has no glyph for U+"+scalar.ToString("X4")+"; provide a matching Font or IMarkTextRasterizer");
            }
        }
        public MarkTextCoverage Rasterize(int canvasWidth,int canvasHeight,BrandDraw command)
        {
            if(font==null||!font.dynamic)throw new LivLivError("missing-feature","Owner mark needs a dynamic Font with included font data");
            if(double.IsNaN(command.fontSize)||double.IsInfinity(command.fontSize)||command.fontSize<=0||command.fontSize>2048)
                throw new LivLivError("too-large","Owner mark font size exceeds its budget");
            // Canvas fillText replaces ASCII whitespace with spaces; it does not interpret rich text or wrap lines.
            string text=Normalize(command.text);
            int size=(int)Math.Ceiling(command.fontSize);
            if(text.Length>8192||(long)text.Length*size*size>16777216)throw new LivLivError("too-large","Owner mark glyph request exceeds its budget");
            var empty=new MarkTextCoverage{alpha=Array.Empty<float>()};if(text.Length==0)return empty;
            CheckFont(text,size);
            font.RequestCharactersInTexture(text,size,style);
            using(var generator=new TextGenerator())
            {
                var settings=new TextGenerationSettings{font=font,fontSize=size,fontStyle=style,color=Color.white,scaleFactor=1,lineSpacing=1,
                    textAnchor=command.textBaseline=="top"?TextAnchor.UpperRight:TextAnchor.LowerRight,
                    pivot=new Vector2(1,command.textBaseline=="top"?1:0),generationExtents=new Vector2(canvasWidth,size*2),
                    horizontalOverflow=HorizontalWrapMode.Overflow,verticalOverflow=VerticalWrapMode.Overflow,
                    richText=false,generateOutOfBounds=true,updateBounds=true,alignByGeometry=false};
                if(!generator.Populate(text,settings))throw new LivLivError("missing-feature","Owner mark text generation failed");
                var generated=generator.verts;int count=generated.Count;if(count==0)return empty;
                var vertices=new Vector3[count];var uv=new Vector2[count];var indices=new int[count/4*6];
                double scale=command.fontSize/size,minX=double.PositiveInfinity,minY=minX,maxX=double.NegativeInfinity,maxY=maxX;
                for(int i=0;i<count;i++)
                {
                    var v=generated[i];float x=(float)(command.x+v.position.x*scale),y=(float)(command.y-v.position.y*scale);
                    vertices[i]=new Vector3(x,y);uv[i]=v.uv0;minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                    if(i%4==0){int at=i/4*6;indices[at]=i;indices[at+1]=i+1;indices[at+2]=i+2;indices[at+3]=i;indices[at+4]=i+2;indices[at+5]=i+3;}
                }
                int halo=command.shadowColor==null?0:(int)Math.Ceiling(command.blur*1.5),pad=halo+2;
                // Preserve off-canvas glyph ink needed by the blur; BrandRasterizer clips
                // only the final composited pixels, as it does for logo geometry.
                int left=Math.Max(-halo,(int)Math.Floor(minX)-pad),top=Math.Max(-halo,(int)Math.Floor(minY)-pad);
                int right=Math.Min(canvasWidth+halo,(int)Math.Ceiling(maxX)+pad),bottom=Math.Min(canvasHeight+halo,(int)Math.Ceiling(maxY)+pad);
                if(right<=left||bottom<=top)return empty;
                int width=right-left,height=bottom-top;
                if((long)width*height>16777216||width>SystemInfo.maxTextureSize||height>SystemInfo.maxTextureSize)throw new LivLivError("too-large","Owner mark coverage exceeds its GPU budget");
                Mesh mesh=null;Material material=null;RenderTexture target=null;Texture2D pixels=null;var active=RenderTexture.active;bool srgb=GL.sRGBWrite;
                try
                {
                    var shader=Resources.Load<Shader>("LivLivTextCoverage");if(shader==null||!shader.isSupported)throw new LivLivError("missing-feature","Owner mark coverage shader unavailable");
                    mesh=new Mesh{name="LivLiv text coverage",hideFlags=HideFlags.HideAndDontSave};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();
                    material=new Material(shader){name="LivLiv text coverage",hideFlags=HideFlags.HideAndDontSave};material.SetTexture("_MainTex",font.material.mainTexture);
                    target=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear){name="LivLiv text coverage",hideFlags=HideFlags.HideAndDontSave};
                    if(!target.Create())throw new LivLivError("missing-feature","Owner mark coverage allocation failed");
                    var projection=GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(left,right,bottom,top,-1,1),true);
                    material.SetVector("_View",new Vector4(projection.m00,projection.m11,projection.m03,projection.m13));
                    using(var cmd=new CommandBuffer{name="LivLiv text coverage"})
                    {cmd.SetRenderTarget(target);cmd.SetViewport(new Rect(0,0,width,height));cmd.ClearRenderTarget(false,true,Color.clear);cmd.DrawMesh(mesh,Matrix4x4.identity,material);GL.sRGBWrite=false;Graphics.ExecuteCommandBuffer(cmd);}
                    RenderTexture.active=target;pixels=new Texture2D(width,height,TextureFormat.RGBA32,false,true);pixels.ReadPixels(new Rect(0,0,width,height),0,0,false);
                    var rgba=pixels.GetPixels32();var alpha=new float[width*height];
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)alpha[y*width+x]=rgba[(height-1-y)*width+x].a/255f;
                    return new MarkTextCoverage{left=left,top=top,width=width,height=height,alpha=alpha};
                }
                finally
                {
                    RenderTexture.active=active;GL.sRGBWrite=srgb;
                    if(target!=null){target.Release();TextureDecoder.Destroy(target);}TextureDecoder.Destroy(pixels);TextureDecoder.Destroy(material);TextureDecoder.Destroy(mesh);
                }
            }
        }
    }
}
