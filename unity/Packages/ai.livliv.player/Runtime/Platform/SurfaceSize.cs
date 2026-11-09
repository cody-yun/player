// src/runtime/puppet.ts render(): round(client size * min(maxDpr, devicePixelRatio || 1)).
// Unity's surface supplies logical size/density; explicit GPU budgets bound allocation afterward.
using System;
namespace LivLiv.Platform
{
    public readonly struct SurfaceSize
    {
        public readonly int width,height;
        public readonly bool limited;
        public bool Renderable=>width>0&&height>0;
        private SurfaceSize(int width,int height,bool limited){this.width=width;this.height=height;this.limited=limited;}
        public static SurfaceSize Calculate(double width,double height,double density,double maxPixelRatio=2,int maxSide=8192,long maxPixels=4194304)
        {
            if(!Finite(width)||!Finite(height)||width<0||height<0)throw new ArgumentOutOfRangeException(nameof(width),"Surface size must be finite and nonnegative");
            if(!Finite(maxPixelRatio)||maxPixelRatio<=0)throw new ArgumentOutOfRangeException(nameof(maxPixelRatio));
            if(!Finite(density)||density<0)throw new ArgumentOutOfRangeException(nameof(density));
            if(maxSide<1||maxPixels<1)throw new ArgumentOutOfRangeException(nameof(maxPixels));
            double ratio=Math.Min(maxPixelRatio,density==0?1:density);
            // JS Math.round rounds nonnegative ties upward, unlike Math.Round's default ties-to-even.
            double w=Math.Floor(width*ratio+.5),h=Math.Floor(height*ratio+.5);
            if(w>int.MaxValue||h>int.MaxValue)throw new ArgumentOutOfRangeException(nameof(width),"Surface exceeds integer pixel dimensions");
            if(w==0||h==0)return new SurfaceSize((int)w,(int)h,false);
            double scale=Math.Min(1,Math.Min((double)maxSide/Math.Max(w,h),Math.Sqrt(maxPixels/(w*h))));
            int x=Math.Max(1,(int)Math.Floor(w*scale)),y=Math.Max(1,(int)Math.Floor(h*scale));
            // Extreme aspect ratios can round the thin side up to one pixel after scaling.
            if((long)x*y>maxPixels){if(x>=y)x=(int)(maxPixels/y);else y=(int)(maxPixels/x);}
            return new SurfaceSize(x,y,scale<1);
        }
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
    }
}
