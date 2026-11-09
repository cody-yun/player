// src/runtime/puppet.ts drawMasks(): drawing-buffer bounds, 2px guard, grow-only 64px storage.
using System;
using System.Collections.Generic;
namespace LivLiv.Platform
{
    public readonly struct MaskBounds
    {
        public readonly int x,y,width,height;
        public bool Renderable=>width>0&&height>0;
        private MaskBounds(int x,int y,int width,int height){this.x=x;this.y=y;this.width=width;this.height=height;}
        public static MaskBounds Calculate(IEnumerable<float[]> eyes,int w,int h,double sx,double sy,double ox,double oy)
        {
            double x0=double.PositiveInfinity,y0=double.PositiveInfinity,x1=double.NegativeInfinity,y1=double.NegativeInfinity;
            foreach(var points in eyes)for(int i=0;i<points.Length;i+=2)
            {
                double X=(points[i]*sx+ox+1)*w/2,Y=(1-(points[i+1]*sy+oy))*h/2;
                x0=Math.Min(x0,X);x1=Math.Max(x1,X);y0=Math.Min(y0,Y);y1=Math.Max(y1,Y);
            }
            x0=Math.Max(0,Math.Floor(x0)-2);y0=Math.Max(0,Math.Floor(y0)-2);
            x1=Math.Min(w,Math.Ceiling(x1)+2);y1=Math.Min(h,Math.Ceiling(y1)+2);
            if(!(x1>x0&&y1>y0))return default;
            return new MaskBounds((int)x0,(int)y0,(int)(x1-x0),(int)(y1-y0));
        }
        public static int Grow(int previous,int required)=>Math.Max(previous,checked((int)Math.Ceiling(required/64d)*64));
    }
}
