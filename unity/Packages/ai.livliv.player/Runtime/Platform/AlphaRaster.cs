// Canvas drawImage alpha readback boundary used by puppet.ts alphaGrid.
// Top-down rows; software mip preparation, bilinear filtering and source-over.
// Canvas image decode caches can resize before drawing (unlike drawing from another canvas).
// This deterministic adapter follows that two-stage path; browser/cache-specific AA can differ.
// Q15 interpolation adapted from Skia, Copyright 2018 Google LLC, BSD-3-Clause.
// The attribution/license is retained in ThirdParty/Skia-LICENSE.md.
using System;
using System.Collections.Generic;
using LivLiv.Runtime;

namespace LivLiv.Platform
{
    public sealed class AlphaImage
    {
        public readonly int width,height;
        public readonly byte[] alpha;
        public AlphaImage(int width,int height,byte[] alpha)
        {if(width<=0||height<=0||alpha==null||alpha.Length!=(long)width*height)throw new ArgumentException("Invalid alpha image");this.width=width;this.height=height;this.alpha=alpha;}
    }
    public static class AlphaRaster
    {
        // Mono may retain excess precision in float locals. A bit boundary makes the Skia scalar
        // stores identical on .NET, Mono and IL2CPP without allocating one-element float arrays.
        private static double F(double value)=>BitConverter.Int32BitsToSingle(BitConverter.SingleToInt32Bits((float)value));
        public static byte[] Grid(IReadOnlyList<AlphaImage> images,LayerLattice grid)
            => Draw(images,grid.cols,grid.rows,grid.ox,grid.oy,grid.w,grid.h);
        public static byte[] Draw(IReadOnlyList<AlphaImage> images,int width,int height,double x,double y,double w,double h)
        {
            if(width<=0||height<=0||(long)width*height>16777216||w<=0||h<=0)throw new ArgumentException("Invalid alpha raster bounds");
            var output=new byte[checked(width*height)];
            int x0=Math.Max(0,(int)Math.Floor(x)),y0=Math.Max(0,(int)Math.Floor(y)),x1=Math.Min(width,(int)Math.Ceiling(x+w)),y1=Math.Min(height,(int)Math.Ceiling(y+h));
            foreach(var original in images)
            {
            var image=Prepare(original,w,h);
            for(int r=y0;r<y1;r++)for(int c=x0;c<x1;c++)
            {
                // Canvas drawImage's axis-aligned image clip tests pixel centers, not fractional area.
                if(c+.5<=x||c+.5>=x+w||r+.5<=y||r+.5>=y+h)continue;
                double a=Sample(image,((c+.5-x)/w)*image.width-.5,((r+.5-y)/h)*image.height-.5);
                int p=r*width+c;output[p]=(byte)Math.Min(255,Math.Floor(a+output[p]*(1-a/255)));
            }
            }
            return output;
        }
        private static double Sample(AlphaImage image,double sx,double sy,bool quantize=true)
        {
            sx=F(sx);sy=F(sy);
            sx=Math.Max(0,Math.Min(image.width-1,sx));sy=Math.Max(0,Math.Min(image.height-1,sy));
            int ix=(int)Math.Floor(sx),iy=(int)Math.Floor(sy),jx=Math.Min(image.width-1,ix+1),jy=Math.Min(image.height-1,iy+1);
            double tx=sx-ix,ty=sy-iy;if(quantize){tx=Math.Floor(tx*16)/16;ty=Math.Floor(ty*16)/16;}
            return (image.alpha[iy*image.width+ix]*(1-tx)+image.alpha[iy*image.width+jx]*tx)*(1-ty)+(image.alpha[jy*image.width+ix]*(1-tx)+image.alpha[jy*image.width+jx]*tx)*ty;
        }
        private static AlphaImage Prepare(AlphaImage original,double width,double height)
        {
            int targetW=Math.Max(1,(int)Math.Floor(width+.5)),targetH=Math.Max(1,(int)Math.Floor(height+.5));
            int step=1,w=original.width,h=original.height;
            while(w>1||h>1)
            {
                int next=step*2,nw=(original.width+next-1)/next,nh=(original.height+next-1)/next;
                if(nw<targetW||nh<targetH)break;w=nw;h=nh;step=next;
            }
            if(step==1)return original;
            // Geometric mean matches the Canvas backend used for upstream reference capture.
            // Skia also has a newer min-axis mode; keep this boundary explicit for future drift.
            double scale=Math.Sqrt((double)w/original.width*((double)h/original.height));
            int level=Math.Max(0,(int)Math.Floor(-Math.Log(scale,2)+1e-10));var mip=original;
            for(int i=0;i<level;i++)mip=Downsample(mip);
            double sx=F(F(1/F((double)w/original.width))*F((double)mip.width/original.width)),sy=F(F(1/F((double)h/original.height))*F((double)mip.height/original.height));
            var data=new byte[w*h];for(int y=0;y<h;y++)for(int x=0;x<w;x++)data[y*w+x]=Medium(mip,F((x+.5)*sx),F((y+.5)*sy));
            return new AlphaImage(w,h,data);
        }
        // Skia lowp bilerp uses Q15 multiply/round at each axis, not one final float rounding.
        // See ThirdParty/Skia-LICENSE.md and docs/unity/GRAPHICS.md for source attribution.
        private static byte Medium(AlphaImage image,double cx,double cy)
        {
            int qx=(int)Math.Floor(F(F(65536*F(cx))+.5))-32768,qy=(int)Math.Floor(F(F(65536*F(cy))+.5))-32768;
            int sx=qx>>16,sy=qy>>16,tx=unchecked((short)(qx^0x8000)),ty=unchecked((short)(qy^0x8000));
            int x0=Math.Max(0,Math.Min(image.width-1,sx)),x1=Math.Max(0,Math.Min(image.width-1,sx+1));
            int y0=Math.Max(0,Math.Min(image.height-1,sy)),y1=Math.Max(0,Math.Min(image.height-1,sy+1));
            int X(int left,int right)=>((((tx*((right-left)<<7)+16384)>>15)+((right+left)<<7)+1)>>1);
            int top=X(image.alpha[y0*image.width+x0],image.alpha[y0*image.width+x1]),bottom=X(image.alpha[y1*image.width+x0],image.alpha[y1*image.width+x1]);
            return (byte)((((ty*(bottom-top)+16384)>>15)+bottom+top+128)>>8);
        }
        private static AlphaImage Downsample(AlphaImage image)
        {
            int w=Math.Max(1,image.width/2),h=Math.Max(1,image.height/2);
            int nx=image.width==1?1:image.width%2==0?2:3,ny=image.height==1?1:image.height%2==0?2:3;
            int divisor=(nx==3?4:nx)*(ny==3?4:ny);var data=new byte[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)
            {
                int sum=0;for(int j=0;j<ny;j++)for(int i=0;i<nx;i++)sum+=image.alpha[(2*y+j)*image.width+2*x+i]*(nx==3&&i==1?2:1)*(ny==3&&j==1?2:1);
                data[y*w+x]=(byte)(sum/divisor);
            }
            return new AlphaImage(w,h,data);
        }
    }
}
