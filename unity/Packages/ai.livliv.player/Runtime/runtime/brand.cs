// Port of src/runtime/brand.ts. Copyright 2026 LivLiv. Apache-2.0.
// Canvas calls become ordered drawing commands. Rasterization is a platform boundary.
using System;
using System.Collections.Generic;

namespace LivLiv.Runtime
{
    public sealed class BrandDraw
    {
        public string kind, path, color, shadowColor, text, textBaseline;
        public double sx=1,sy=1,tx,ty,width,alpha=1,blur,x,y,fontSize;
        public double[] ellipse;
    }
    public struct CornerSpot
    {
        public double right,y,logo,text;
        public bool top;
    }
    public static class Brand
    {
        public const string BrandMark="LivLiv", Coral="#ff4d5e";
        public static readonly double[] LogoBox={27,44,430,133};
        public const string Letters="M101.5 62.5V141.5Q101.5 158.5 115.5 158.5M145 107.5V158.5M176.3 107.5L200.5 158.5L224.7 107.5M257.5 62.5V141.5Q257.5 158.5 271.5 158.5M303 107.5V158.5M335.3 107.5L359.5 158.5L383.7 107.5";
        public const string Blush="M40.5 122L49.5 142.1M60.5 122L69.5 142.1M421.5 122L412.5 142.1M441.5 122L432.5 142.1";
        public const string Wink="M293.5 69H312.5A8 8 0 0 1 312.5 85H293.5A8 8 0 0 1 293.5 69Z";
        public const double EyeX=145,EyeY=67,EyeRadius=13.5;
        public const double Center=.38,CenterY=.62,CenterAlpha=.18,CenterBlur=.06;
        public const double Corner=.13,CornerMin=64,Margin=.035,Text=.026,Top=.12;
        public const string CenterShadow="rgba(0,0,0,.25)";
        public static double EyeOpen(double ms)
        {
            var f=(ms%5500)/5500;
            if(f<.93)return 1;
            return f<.955 ? 1-(f-.93)/.025*.88 : .12+(f-.955)/.045*.88;
        }
        public static CornerSpot CornerPosition(double w,double h,bool top=false,double px=1)
        {
            var s=Math.Min(w,h);var m=Math.Max(12*px,s*Margin);
            return new CornerSpot { right=w-m,y=top?(h>w?h*Top:m):h-m,top=top,
                logo=Math.Max(CornerMin*px,s*Corner)*LogoBox[3]/LogoBox[2],text=Math.Max(12*px,s*Text) };
        }
        public static void DrawLogo(List<BrandDraw> into,double right,double bottom,double height,string color,double ms=0,string accent=Coral,
            double edge=0,string edgeColor="#222",double alpha=1,string shadowColor=null,double shadowBlur=0)
        {
            var k=height/LogoBox[3];var tx=right-LogoBox[2]*k-LogoBox[0]*k;var ty=bottom-height-LogoBox[1]*k;
            void Draw(string kind,string path,string ink,double width=0,double[] ellipse=null)
            { into.Add(new BrandDraw { kind=kind,path=path,color=ink,width=width,ellipse=ellipse,sx=k,sy=k,tx=tx,ty=ty,alpha=alpha,shadowColor=shadowColor,blur=shadowBlur }); }
            var eye=new[]{EyeX,EyeY,EyeRadius,EyeRadius*EyeOpen(ms)};
            if(edge!=0)
            {
                Draw("stroke",Letters,edgeColor,25+edge*2);Draw("stroke",Blush,edgeColor,12.5+edge*2);
                Draw("stroke",null,edgeColor,edge*2,eye);Draw("stroke",Wink,edgeColor,edge*2);
            }
            Draw("stroke",Letters,color,25);Draw("stroke",Blush,accent,12.5);
            Draw("fill",null,accent,0,eye);Draw("fill",Wink,accent);
        }
        public static void DrawCornerLogo(List<BrandDraw> into,double w,double h,double ms=0,double px=1,bool top=false,bool outline=false)
        {
            var c=CornerPosition(w,h,top,px);
            DrawLogo(into,c.right,c.top?c.y+c.logo:c.y,c.logo,outline?"#fff":"rgba(255,255,255,.95)",ms,Coral,outline?9:0,"#222",1,
                outline?null:"rgba(0,0,0,.55)",outline?0:c.logo*.12);
        }
        public static void DrawMarkText(List<BrandDraw> into,double w,double h,string text,double px=1,bool top=false)
        {
            var c=CornerPosition(w,h,top,px);
            into.Add(new BrandDraw { kind="text",text=text,x=c.right,y=c.y,fontSize=c.text,textBaseline=c.top?"top":"bottom",
                color="rgba(255,255,255,.85)",shadowColor="rgba(0,0,0,.6)",blur=c.text*.25 });
        }
        public static void DrawWatermark(List<BrandDraw> into,double w,double h,double ms=0,double px=1,bool top=false)
        {
            var cw=Math.Min(w,h)*Center;var ch=cw*LogoBox[3]/LogoBox[2];
            DrawLogo(into,(w+cw)/2,h*CenterY+ch/2,ch,"#fff",ms,"#fff",0,"#222",CenterAlpha,CenterShadow,ch*CenterBlur);
            DrawCornerLogo(into,w,h,ms,px,top);
        }
    }
}
