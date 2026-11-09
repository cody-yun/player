using System.Collections.Generic;
using LivLiv.Platform;
using LivLiv.Runtime;
using UnityEngine;

namespace LivLiv.Unity
{
    public static class LayerSampling
    {
        public static byte[] CanvasRgba(Color32[] pixels,int width,int height,bool webp=false)
        {
            var output=new byte[width*height*4];
            byte Channel(byte value,byte alpha)=>alpha==0?(byte)0:(byte)System.Math.Min(255,((value*alpha+(webp?0:127))/255*255+alpha/2)/alpha);
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var c=pixels[(height-y-1)*width+x];int k=4*(y*width+x);
                output[k]=Channel(c.r,c.a);output[k+1]=Channel(c.g,c.a);output[k+2]=Channel(c.b,c.a);output[k+3]=c.a;
            }
            return output;
        }
        public static AlphaImage Alpha(Texture2D texture)
        {
            // PNG LoadImage can select ARGB32; raw Color32 reinterpretation would read blue as alpha.
            // GetPixels32 normalizes channels for both PNG and the RGBA32 WebP path.
            var pixels=texture.GetPixels32(0);int w=texture.width,h=texture.height;var alpha=new byte[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)alpha[y*w+x]=pixels[(h-y-1)*w+x].a;
            return new AlphaImage(w,h,alpha);
        }
        public static byte[] Grid(Texture2D main,IReadOnlyList<Texture2D> frames,LayerLattice grid)
        {
            var images=new List<AlphaImage>{Alpha(main)};foreach(var frame in frames)images.Add(Alpha(frame));return AlphaRaster.Grid(images,grid);
        }
    }
}
