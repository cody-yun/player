// Platform adapter for puppet.ts loadBlob/loadImage/texture. PNG/JPEG via Unity, WebP via libwebp.
using System;
using UnityEngine;
using LivLiv.Runtime;
using LivLiv.Platform;

namespace LivLiv.Unity
{
    public sealed class TextureDecoder
    {
        private readonly IWebPDecoder webp;
        public TextureDecoder(IWebPDecoder webp=null) { this.webp=webp??new NativeWebPDecoder(); }
        public long texels { get; private set; }
        public static void Destroy(UnityEngine.Object obj)
        { if (obj == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
        private void Check(int width,int height)
        {
            var most=Math.Min(ModelFormat.Limits.Texture,SystemInfo.maxTextureSize>0?SystemInfo.maxTextureSize:ModelFormat.Limits.Texture);
            if (width<=0 || height<=0) throw new LivLivError("invalid","Invalid image dimensions");
            if (width>most || height>most || texels+(long)width*height>ModelFormat.Limits.Texels) throw new LivLivError("too-large","This model's images exceed the pixel budget");
        }
        private static bool IsWebP(byte[] b) => b.Length>=12 && b[0]=='R' && b[1]=='I' && b[2]=='F' && b[3]=='F' && b[8]=='W' && b[9]=='E' && b[10]=='B' && b[11]=='P';
        private static bool IsPng(byte[] b) => b.Length>=24 && b[0]==137 && b[1]==80 && b[2]==78 && b[3]==71 && b[4]==13 && b[5]==10 && b[6]==26 && b[7]==10;
        private static int Big32(byte[] b,int i) => unchecked((int)((uint)b[i]<<24|(uint)b[i+1]<<16|(uint)b[i+2]<<8|b[i+3]));
        public Texture2D Decode(byte[] bytes,Action<Color32[],int,int,bool> beforePremultiply=null)
        {
            Texture2D texture=null;
            try
            {
                int w,h;
                if (IsWebP(bytes))
                {
                    try
                    {
                        if (!webp.GetInfo(bytes,out w,out h)) throw new LivLivError("invalid","Invalid WebP header");
                    }
                    catch (DllNotFoundException e) { throw new LivLivError("missing-feature","WebP decoder artifact is missing for this platform; install the matching native/WASM library or supply IWebPDecoder",cause:e); }
                    catch (EntryPointNotFoundException e) { throw new LivLivError("missing-feature","WebP decoder has incompatible exports",cause:e); }
                    Check(w,h);
                    var raw=new byte[checked(w*h*4)];
                    if (!webp.DecodeRgba(bytes,raw,w,h)) throw new LivLivError("invalid","Cannot decode WebP");
                    // libwebp rows go top to bottom; Unity's raw texture rows go bottom to top.
                    int stride=w*4; var row=new byte[stride];
                    for (int y=0;y<h/2;y++) { Buffer.BlockCopy(raw,y*stride,row,0,stride); Buffer.BlockCopy(raw,(h-y-1)*stride,raw,y*stride,stride); Buffer.BlockCopy(row,0,raw,(h-y-1)*stride,stride); }
                    texture=new Texture2D(w,h,TextureFormat.RGBA32,true,true);
                    texture.SetPixelData(raw,0); // Only mip 0; Apply below generates the rest.
                }
                else if (IsPng(bytes))
                {
                    w=Big32(bytes,16);h=Big32(bytes,20);Check(w,h);
                    texture=new Texture2D(2,2,TextureFormat.RGBA32,true,true);
                    if (!ImageConversion.LoadImage(texture,bytes,false)) throw new LivLivError("invalid","Cannot decode PNG");
                    if (texture.width!=w || texture.height!=h) throw new LivLivError("invalid","Decoded image dimensions differ from header");
                }
                else if(JpegHeader.Matches(bytes))
                {
                    JpegHeader.Read(bytes,out w,out h);Check(w,h);
                    texture=new Texture2D(2,2,TextureFormat.RGBA32,true,true);
                    if(!ImageConversion.LoadImage(texture,bytes,false))throw new LivLivError("invalid","Cannot decode JPEG");
                    if(texture.width!=w||texture.height!=h)throw new LivLivError("invalid","Decoded image dimensions differ from header");
                }
                else throw new LivLivError("invalid","Expected a PNG, WebP or JPEG image");
                // Match UNPACK_PREMULTIPLY_ALPHA_WEBGL before filtering/mipmap generation.
                var pixels=texture.GetPixels32();
                bool webpPremultiply=IsWebP(bytes);
                beforePremultiply?.Invoke(pixels,texture.width,texture.height,webpPremultiply);
                for (int i=0;i<pixels.Length;i++)
                {
                    // Chromium WebP uses libwebp MODE_rgbA (floor); PNG uses rounded Skia premultiplication.
                    int rounding=webpPremultiply?0:127;var c=pixels[i];c.r=(byte)((c.r*c.a+rounding)/255);c.g=(byte)((c.g*c.a+rounding)/255);c.b=(byte)((c.b*c.a+rounding)/255);pixels[i]=c;
                }
                texture.SetPixels32(pixels);texture.Apply(true,false);
                texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Trilinear;
                texture.name="LivLiv decoded texture";texture.hideFlags=HideFlags.HideAndDontSave;
                texels+=(long)texture.width*texture.height;
                return texture;
            }
            catch { Destroy(texture);throw; }
        }
    }
}
