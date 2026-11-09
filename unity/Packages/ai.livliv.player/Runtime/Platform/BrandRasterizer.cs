// Portable adapter for brand.ts Canvas commands. No native or browser dependency.
// Geometry is source-faithful; antialiasing and Gaussian shadow are approximations of Canvas 2D.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using LivLiv.Runtime;

namespace LivLiv.Platform
{
    public static class BrandRasterizer
    {
        private struct Segment { public double x,y,u,v; public Segment(double x,double y,double u,double v){this.x=x;this.y=y;this.u=u;this.v=v;} }
        private static readonly Segment[] Letters=Parse(Brand.Letters),Blush=Parse(Brand.Blush);
        // Top-down premultiplied RGBA8, matching the upstream canvas upload contract.
        public static byte[] Draw(int width,int height,IReadOnlyList<BrandDraw> commands,IMarkTextRasterizer textRasterizer=null)
        {
            if(width<=0||height<=0||(long)width*height>16777216)throw new LivLivError("too-large","Watermark canvas exceeds 16M pixels");
            var output=new byte[checked(width*height*4)];
            foreach(var c in commands)
            {
                if(c.kind=="text")
                {
                    if(textRasterizer==null)throw new LivLivError("missing-feature","Owner text watermark requires a font rasterizer");
                    var t=textRasterizer.Rasterize(width,height,c);
                    int halo=c.shadowColor==null?0:(int)Math.Ceiling(c.blur*1.5);
                    if(t==null||t.left < -halo||t.top < -halo||t.width<0||t.height<0||(long)t.left+t.width>(long)width+halo||(long)t.top+t.height>(long)height+halo||t.alpha==null||(long)t.width*t.height>16777216||(long)t.width*t.height!=t.alpha.Length)
                        throw new LivLivError("invalid","Text rasterizer returned invalid coverage bounds");
                    foreach(float a in t.alpha)if(float.IsNaN(a)||a<0||a>1)throw new LivLivError("invalid","Text rasterizer returned invalid alpha");
                    Paint(output,width,t.left,t.top,t.width,t.height,t.alpha,c);continue;
                }
                if(c.sx<=0||c.sy!=c.sx)throw new LivLivError("invalid","Unsupported watermark transform");
                Segment[] segments=c.path==Brand.Letters?Letters:c.path==Brand.Blush?Blush:null;
                bool wink=c.path==Brand.Wink;
                if(segments==null&&!wink&&c.ellipse==null)throw new LivLivError("invalid","Unknown brand path");
                double minX=double.PositiveInfinity,minY=minX,maxX=double.NegativeInfinity,maxY=maxX;
                void Bound(double x,double y){minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);}
                if(segments!=null)foreach(var s in segments){Bound(s.x,s.y);Bound(s.u,s.v);}
                else if(wink){Bound(285.5,69);Bound(320.5,85);}
                else {var e=c.ellipse;Bound(e[0]-e[2],e[1]-e[3]);Bound(e[0]+e[2],e[1]+e[3]);}
                double radius=c.kind=="stroke"?c.width/2:0, pad=radius*c.sx+2;
                int blurRadius=c.shadowColor==null?0:(int)Math.Ceiling(c.blur*1.5);
                // Canvas clips the blurred result, not the source ink. Keep one kernel radius
                // beyond the viewport so off-canvas geometry can cast a shadow onto it.
                int left=Math.Max(-blurRadius,(int)Math.Floor(minX*c.sx+c.tx-pad-blurRadius)),top=Math.Max(-blurRadius,(int)Math.Floor(minY*c.sy+c.ty-pad-blurRadius));
                int right=Math.Min(width+blurRadius,(int)Math.Ceiling(maxX*c.sx+c.tx+pad+blurRadius)),bottom=Math.Min(height+blurRadius,(int)Math.Ceiling(maxY*c.sy+c.ty+pad+blurRadius));
                int w=right-left,h=bottom-top;if(w<=0||h<=0)continue;
                var mask=new float[w*h];
                for(int y=0;y<h;y++)for(int x=0;x<w;x++)
                {
                    double px=(left+x+.5-c.tx)/c.sx,py=(top+y+.5-c.ty)/c.sy;
                    if(px<minX-radius-1/c.sx||px>maxX+radius+1/c.sx||py<minY-radius-1/c.sy||py>maxY+radius+1/c.sy)continue;
                    int hits=0;
                    for(int iy=0;iy<4;iy++)for(int ix=0;ix<4;ix++)
                    {
                        double xx=px+(ix*.25-.375)/c.sx,yy=py+(iy*.25-.375)/c.sy;
                        bool inside=false;
                        if(segments!=null){foreach(var s in segments)if(Distance(xx,yy,s)<=radius){inside=true;break;}}
                        else if(wink){double d=Distance(xx,yy,new Segment(293.5,77,312.5,77));inside=c.kind=="stroke"?Math.Abs(d-8)<=radius:d<=8;}
                        else
                        {
                            var e=c.ellipse;double dx=xx-e[0],dy=yy-e[1];
                            if(c.kind=="fill")inside=dx*dx/(e[2]*e[2])+dy*dy/(e[3]*e[3])<=1;
                            else inside=EllipseDistance(dx,dy,e[2],e[3])<=radius;
                        }
                        if(inside)hits++;
                    }
                    mask[y*w+x]=hits/16f;
                }
                Paint(output,width,left,top,w,h,mask,c);
            }
            return output;
        }
        private static void Paint(byte[] output,int width,int left,int top,int w,int h,float[] mask,BrandDraw c)
        {
            if(w==0||h==0)return;
            var ink=Color(c.color);
            if(c.shadowColor!=null)
            {
                var shadow=Color(c.shadowColor);shadow[3]*=ink[3];
                Composite(output,width,left,top,w,h,Blur(mask,w,h,c.blur/2),shadow,c.alpha);
            }
            Composite(output,width,left,top,w,h,mask,ink,c.alpha);
        }
        private static double Distance(double x,double y,Segment s)
        {
            double dx=s.u-s.x,dy=s.v-s.y,len=dx*dx+dy*dy;
            double t=len==0?0:Math.Max(0,Math.Min(1,((x-s.x)*dx+(y-s.y)*dy)/len));
            dx=x-s.x-t*dx;dy=y-s.y-t*dy;return Math.Sqrt(dx*dx+dy*dy);
        }
        private static double EllipseDistance(double x,double y,double rx,double ry)
        {
            // Outline is infrequent (GIF-style corner); polygon distance is stable at fully closed blink.
            double best=double.PositiveInfinity,px=rx,py=0;
            for(int i=1;i<=128;i++){double a=i*Math.PI/64,nx=rx*Math.Cos(a),ny=ry*Math.Sin(a);best=Math.Min(best,Distance(x,y,new Segment(px,py,nx,ny)));px=nx;py=ny;}
            return best;
        }
        private static float[] Blur(float[] mask,int w,int h,double sigma)
        {
            if(sigma<=.01)return mask;
            int r=(int)Math.Ceiling(3*sigma);var kernel=new double[r*2+1];double total=0;
            for(int k=-r;k<=r;k++){kernel[k+r]=Math.Exp(-k*k/(2*sigma*sigma));total+=kernel[k+r];}
            for(int k=0;k<kernel.Length;k++)kernel[k]/=total;
            var temp=new float[mask.Length];var result=new float[mask.Length];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){double a=0;for(int k=-r;k<=r;k++)if(x+k>=0&&x+k<w)a+=mask[y*w+x+k]*kernel[k+r];temp[y*w+x]=(float)a;}
            for(int y=0;y<h;y++)for(int x=0;x<w;x++){double a=0;for(int k=-r;k<=r;k++)if(y+k>=0&&y+k<h)a+=temp[(y+k)*w+x]*kernel[k+r];result[y*w+x]=(float)a;}
            return result;
        }
        private static void Composite(byte[] dst,int stride,int left,int top,int w,int h,float[] mask,double[] color,double alpha)
        {
            int height=dst.Length/(stride*4);
            for(int y=Math.Max(0,-top);y<Math.Min(h,height-top);y++)for(int x=Math.Max(0,-left);x<Math.Min(w,stride-left);x++)
            {
                double a=mask[y*w+x]*color[3]*alpha;if(a<=0)continue;int p=((top+y)*stride+left+x)*4;
                for(int k=0;k<3;k++)dst[p+k]=(byte)Math.Min(255,Math.Floor(color[k]*a*255+dst[p+k]*(1-a)+.5));
                dst[p+3]=(byte)Math.Min(255,Math.Floor(a*255+dst[p+3]*(1-a)+.5));
            }
        }
        private static double[] Color(string value)
        {
            if(value.StartsWith("#",StringComparison.Ordinal))
            {
                string hex=value.Substring(1);if(hex.Length==3)hex=$"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                return new[]{int.Parse(hex.Substring(0,2),NumberStyles.HexNumber)/255d,int.Parse(hex.Substring(2,2),NumberStyles.HexNumber)/255d,int.Parse(hex.Substring(4,2),NumberStyles.HexNumber)/255d,1};
            }
            var p=value.Substring(5,value.Length-6).Split(',');return new[]{double.Parse(p[0],CultureInfo.InvariantCulture)/255,double.Parse(p[1],CultureInfo.InvariantCulture)/255,double.Parse(p[2],CultureInfo.InvariantCulture)/255,double.Parse(p[3],CultureInfo.InvariantCulture)};
        }
        private static Segment[] Parse(string path)
        {
            var tokens=Regex.Matches(path,"[MLVQ]|-?[0-9]+(?:\\.[0-9]+)?");int at=0;double x=0,y=0;
            double N()=>double.Parse(tokens[at++].Value,CultureInfo.InvariantCulture);
            var segments=new List<Segment>();
            while(at<tokens.Count)
            {
                string op=tokens[at++].Value;
                if(op=="M"){x=N();y=N();continue;}
                double u,v;
                if(op=="Q")
                {
                    double cx=N(),cy=N(),ex=N(),ey=N(),sx=x,sy=y;
                    for(int i=1;i<=32;i++){double t=i/32d;u=(1-t)*(1-t)*sx+2*(1-t)*t*cx+t*t*ex;v=(1-t)*(1-t)*sy+2*(1-t)*t*cy+t*t*ey;segments.Add(new Segment(x,y,u,v));x=u;y=v;}continue;
                }
                if(op=="V"){u=x;v=N();}else if(op=="L"){u=N();v=N();}else throw new InvalidOperationException("Unsupported brand segment "+op);
                segments.Add(new Segment(x,y,u,v));x=u;y=v;
            }
            return segments.ToArray();
        }
    }
}
