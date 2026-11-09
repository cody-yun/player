// Port of src/runtime/puppet.ts. Copyright 2026 LivLiv. Apache-2.0.
// Numeric mesh/build, dynamics and deformation kernel. Canvas sampling and GPU resources
// live in Platform adapters. Numeric image helpers and keyform preparation retain source boundaries.
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace LivLiv.Runtime
{
    public sealed class LayerLattice
    {
        public int cell,gx0,gy0,cols,rows;
        public double ox,oy,w,h;
        public LayerLattice(JObject model,JObject layer)
        {
            cell=ModelFormat.LatticeCell((double)model["canvas"][0],(double)model["canvas"][1]);
            double x=(double)layer["x"],y=(double)layer["y"],width=(double)layer["w"],height=(double)layer["h"];
            gx0=(int)Math.Floor(x/cell);gy0=(int)Math.Floor(y/cell);
            cols=(int)Math.Ceiling((x+width)/cell)-gx0;rows=(int)Math.Ceiling((y+height)/cell)-gy0;
            ox=(x-gx0*cell)/cell;oy=(y-gy0*cell)/cell;w=width/cell;h=height/cell;
        }
    }
    public sealed class PuppetSkin
    {
        public int B;
        public short[] c,seg;
        public float[] t,ux,uy,w,sum;
    }
    public sealed class PuppetLayer
    {
        public JObject def;
        public float[] rest,output,uv;
        public int[] tri;
        public float[] wHead,wPhys1,wPhys2,dep,wBrow,wFront,wJaw,browCap,wNeck,wTwist,fol,folB,alt;
        public sbyte[] browSide;
        public PuppetSkin skin,altSkin;
        public float[] pivY,outB;
        public double? pivC;
        public double soft;
        public int maskCh=-1,stencilRef;
        public float[][] kf;
    }
    public sealed class PuppetSpring { public double x,y,vx,vy; }
    public sealed class PuppetBody { public double yaw,roll; }
    public sealed class PuppetBust { public double x,y,vx,vy,px,py; }
    public sealed class PuppetDrive { public double x,y,vx,vy,ax,ay;public bool started; }
    public sealed class PuppetHead { public double x,y,roll; }
    public sealed class PuppetChain
    {
        public JObject def;
        public float[] R,p,q,len,s,disp,beta;
        public double[] W={0,0},V={0,0},acc={0,0};
        public double rho,phase;
        public bool started,moving;
    }
    public sealed partial class Puppet
    {
        public JObject model { get; private set; }
        public JObject rig { get; private set; }=DefaultRig();
        public JObject tuning { get; private set; }=DefaultTuning();
        public double procJaw=.015,chinNeck=.08,neckFollow=.2,neckFollowY=.6;
        public bool chinFollow=true;
        public double[] chinNeckX={.2,.34},neckFollowX={.6,1.3};
        public string[] frameKind={"closed","half","open"};
        public double[] frameJaw={0,0,0};
        private JObject[] chainDefs=Array.Empty<JObject>();
        private float[][] chainRest=Array.Empty<float[]>();
        private static double N(JToken o,string key,double fallback=double.NaN)=>o?[key]==null||o[key].Type==JTokenType.Null?fallback:(double)o[key];
        private static double Smooth(double a,double b,double x)=>Params.Smooth(a,b,x);
        private static double Hypot(double x,double y)=>Math.Sqrt(x*x+y*y);
        public static JObject DefaultRig()=>JObject.Parse(@"{'yaw':0.27,'pitch':0.22,'roll':0.5,'bulge':0.6,'hair':{'front':[140,11,0.022,70,7],'back':[48,5.5,0.06,24,3.5]},'hair_limit':0.25,'wind':1,'breath':1,'sway':1,'brow':0.06,'body':{'yaw':0.35,'roll':0.3,'breath':1,'bust':0}}");
        public static JObject DefaultTuning()=>JObject.Parse(@"{'turn':1,'nod':1,'hair':1,'blink':1,'expr':1,'mouth':1,'breath':1,'gaze':1}");
        private static JObject Spread(JObject defaults,JToken custom)
        {if(custom is JObject obj)foreach(var p in obj.Properties())defaults[p.Name]=p.Value.DeepClone();return defaults;}
        // Definition stage of upstream build(). Call before building layer samples; no GPU mutation here.
        public void LoadDefinition(JObject raw)
        {
            model=ModelFormat.ReadModel(raw);cutBottom=(double)model["anchors"]["bottom_y"]>=(double)model["canvas"][1]-4;var layers=(JArray)model["layers"];
            var mouth=layers.FirstOrDefault(l=>(string)l["role"]=="mouth");
            int count=(mouth?["frames"] as JArray)?.Count??3;
            frameKind=Enumerable.Range(0,count).Select(i=>{var v=(mouth?["visemes"] as JArray);string k=v!=null&&i<v.Count?(string)v[i]:null;return !string.IsNullOrEmpty(k)?k:i<3?new[]{"closed","half","open"}[i]:"";}).ToArray();
            var jaw=model["anchors"]["jaw"] as JObject;
            frameJaw=frameKind.Select(k=>jaw!=null&&k!="closed"&&(k!="half"||N(jaw,"open",0)>0)?N(jaw,k,0):0).ToArray();
            var amount=procJaw>0?N(model["rig"],"jaw",procJaw):0;
            if(jaw!=null&&amount>0&&!frameJaw.Any(j=>j>0))
            {
                double fh=(double)model["anchors"]["face"][3]-(double)model["anchors"]["face"][1];
                frameJaw=frameKind.Select(k=>amount*fh*(k=="half"?.45:k=="open"?1:k=="round"?.7:k=="wide"?.35:0)).ToArray();
            }
            rig=Spread(DefaultRig(),model["rig"]);rig["hair"]=Spread((JObject)DefaultRig()["hair"],model["rig"]?["hair"]);rig["body"]=Spread((JObject)DefaultRig()["body"],model["rig"]?["body"]);
            tuning=Spread(DefaultTuning(),model["tuning"]);
            chainDefs=(model["rig"]?["chains"] as JArray??new JArray()).Cast<JObject>().ToArray();
            chainRuns=chainDefs.Select(ChainRun).ToArray();chainRest=chainRuns.Select(c=>c.R).ToArray();
            vis.Clear();for(int i=0;i<frameKind.Length;i++)if(frameKind[i]=="round"||frameKind[i]=="wide"||frameKind[i]=="shut")vis[frameKind[i]]=i;
            expr.Clear();ResetMouth();talkLeft=0;ResetPhysics();this.layers.Clear();keys.Clear();kfPair=null;nasal.Clear();
        }
        // alphaGrid(): platform draws main + mouth frames onto cols x rows first (source-over).
        // Keep binary threshold and 3x3 dilation here, shared by every platform.
        public static byte[] AlphaGrid(byte[] alpha,int cols,int rows)
        {
            if(alpha.Length!=checked(cols*rows))throw new ArgumentException("Wrong alpha grid dimensions",nameof(alpha));
            var occ=new byte[alpha.Length];for(int i=0;i<occ.Length;i++)occ[i]=(byte)(alpha[i]>0?1:0);
            var output=(byte[])occ.Clone();
            for(int r=0;r<rows;r++)for(int c=0;c<cols;c++)
            {
                if(occ[r*cols+c]!=0)continue;
                for(int dr=-1;dr<=1;dr++)for(int dc=-1;dc<=1;dc++)
                {int rr=r+dr,cc=c+dc;if(rr>=0&&rr<rows&&cc>=0&&cc<cols&&occ[rr*cols+cc]!=0)output[r*cols+c]=1;}
            }
            return output;
        }
        public PuppetLayer BuildLayer(JObject def,byte[] alpha,float[] fol=null,float[] folB=null,JObject bangs=null)
        {
            if(model==null)throw new InvalidOperationException("Load a model definition first");
            var grid=new LayerLattice(model,def);var occ=AlphaGrid(alpha,grid.cols,grid.rows);
            var vid=new int[(grid.cols+1)*(grid.rows+1)];for(int i=0;i<vid.Length;i++)vid[i]=-1;
            var pos=new List<float>();var uv=new List<float>();var idx=new List<int>();
            int Vert(int c,int r)
            {
                int k=r*(grid.cols+1)+c;if(vid[k]>=0)return vid[k];vid[k]=pos.Count/2;
                double x=(grid.gx0+c)*grid.cell,y=(grid.gy0+r)*grid.cell;pos.Add((float)x);pos.Add((float)y);
                uv.Add((float)((x-N(def,"x"))/N(def,"w")));uv.Add((float)((y-N(def,"y"))/N(def,"h")));return vid[k];
            }
            for(int r=0;r<grid.rows;r++)for(int c=0;c<grid.cols;c++)if(occ[r*grid.cols+c]!=0)
            {int a=Vert(c,r),b=Vert(c+1,r),d=Vert(c,r+1),e=Vert(c+1,r+1);idx.AddRange(new[]{a,b,d,b,e,d});}
            int n=pos.Count/2;string group=(string)def["group"],role=(string)def["role"]??"",id=(string)def["id"];
            if((fol!=null&&fol.Length!=n)||(folB!=null&&folB.Length!=n))throw new ArgumentException("Coverage must have one value per lattice vertex");
            var aModel=model["anchors"];var hd=aModel["head"];var jaw=aModel["jaw"] as JObject;var nt=model["rig"]?["neck"];
            var layer=new PuppetLayer {def=def,rest=pos.ToArray(),output=new float[pos.Count],uv=uv.ToArray(),tri=idx.ToArray(),
                wHead=new float[n],wPhys1=new float[n],wPhys2=new float[n],dep=new float[n],wBrow=new float[n],wFront=new float[n],wJaw=new float[n],
                browSide=Enumerable.Repeat((sbyte)-1,n).ToArray(),browCap=Enumerable.Repeat(float.PositiveInfinity,n).ToArray(),
                wNeck=neckFollow>0&&group=="body"?new float[n]:null,wTwist=nt!=null&&N(nt,"twist",0)>0&&group=="body"?new float[n]:null,
                fol=fol,folB=folB,alt=fol!=null||folB!=null?new float[n*5]:null};
            var brows=aModel["brows"] as JArray;var browLines=aModel["brow_lines"] as JArray;
            bool browLayer=((JArray)model["layers"]).Any(l=>(string)l["id"]=="brows"),drops=frameJaw.Any(j=>j>0);
            double cx=N(hd,"cx"),cy=N(hd,"cy"),rx=N(hd,"rx"),ry=N(hd,"ry"),neck=N(aModel,"neck_y");
            double fw=(double)aModel["face"][2]-(double)aModel["face"][0],fh=(double)aModel["face"][3]-(double)aModel["face"][1];
            double hairTop=neck-.1*ry,hairLen=Math.Max(1,N(aModel,"bottom_y")-hairTop);
            for(int i=0;i<n;i++)
            {
                double x=layer.rest[2*i],y=layer.rest[2*i+1],rr=Hypot((x-cx)/rx,(y-cy)/ry);
                layer.dep[i]=(float)(group=="head"?N(def,"depth"):group=="hair_front"?N(def,"depth")*Smooth(N(def,"fall_end",1),N(def,"fall",.55),rr):N(def,"depth")*Smooth(N(def,"fall_end",1),def["fall_end"]==null?.7:N(def,"fall",.7),rr));
                double longHead=1-Smooth(neck+.05*ry,neck+1.1*ry,y),tl=Math.Min(1,Math.Max(0,(y-hairTop)/hairLen)),lp1=Smooth(0,.55,tl),lp2=Math.Pow(Smooth(.25,1,tl),1.3);
                double f=Smooth(.95,.7,rr)*(1-Smooth(neck-.2*ry,neck+.3*ry,y)),tb=Smooth(N(aModel,"hair_root_y")+.25*ry,cy+.7*ry,y);
                if(group=="head")layer.wHead[i]=1;
                else if(group!="body")
                {
                    layer.wHead[i]=(float)(f+(1-f)*longHead);layer.wPhys1[i]=(float)(f*tb+(1-f)*lp1);layer.wPhys2[i]=(float)(f*tb*tb+(1-f)*lp2);layer.wFront[i]=(float)f;
                    if(group=="hair_inner"){layer.wHead[i]=(float)(layer.wHead[i]*.5);layer.wPhys1[i]=(float)(layer.wPhys1[i]*.35);layer.wPhys2[i]=(float)(layer.wPhys2[i]*.35);}
                }
                if(layer.alt!=null&&bangs!=null){int k=5*i;layer.alt[k]=(float)(N(bangs,"depth")*Smooth(1,N(bangs,"fall",.55),rr));layer.alt[k+1]=(float)(f+(1-f)*longHead);layer.alt[k+2]=(float)(f*tb+(1-f)*lp1);layer.alt[k+3]=(float)(f*tb*tb+(1-f)*lp2);layer.alt[k+4]=(float)f;}
                bool hairFront=group=="hair_side"||group=="hair_front",mouthCopy=chinFollow&&role=="mouth";
                if((id=="face"||role=="expr"||hairFront||mouthCopy||role=="neck_shadow")&&jaw!=null&&drops)
                {double vy=Smooth(N(jaw,"top")-.02*ry,N(jaw,"chin"),y)*(hairFront?1-Smooth(N(jaw,"chin"),N(jaw,"chin")+.15*ry,y):1),vx=1-Smooth(.22*fw,.5*fw,Math.Abs(x-cx));layer.wJaw[i]=(float)(vy*vx);}
                if(role=="neck_shadow"&&jaw!=null){double k=1-Smooth(N(jaw,"chin"),N(jaw,"chin")+.3*fh,y);layer.wHead[i]=(float)k;layer.wJaw[i]=(float)(layer.wJaw[i]*k);}
                if(chinFollow&&group=="body"&&jaw!=null)
                {
                    double wy=1-Smooth(N(jaw,"chin"),N(jaw,"chin")+chinNeck*fh,y),wx=1-Smooth(chinNeckX[0]*fw,chinNeckX[1]*fw,Math.Abs(x-cx)),w=wy*wx;
                    if(w>0){layer.wHead[i]=(float)w;if(drops)layer.wJaw[i]=(float)(Smooth(N(jaw,"top")-.02*ry,N(jaw,"chin"),y)*(1-Smooth(.22*fw,.5*fw,Math.Abs(x-cx)))*w);}
                }
                if(layer.wNeck!=null)layer.wNeck[i]=(float)Math.Max(0,neckFollow*(1-Smooth(neck,neck+neckFollowY*fh,y))*(1-Smooth(neckFollowX[0]*fw,neckFollowX[1]*fw,Math.Abs(x-cx)))-layer.wHead[i]);
                if(layer.wTwist!=null){double chin=N(jaw,"chin",neck);layer.wTwist[i]=(float)Math.Max(0,N(nt,"twist")*(1-Smooth(chin,chin+N(nt,"reach",.45)*fh,y))*(1-Smooth(chinNeckX[0]*fw,chinNeckX[1]*fw,Math.Abs(x-cx)))-layer.wHead[i]);}
                if(brows!=null&&group=="head"&&!role.StartsWith("iris_",StringComparison.Ordinal)&&(!browLayer||id=="brows"))
                for(int side=0;side<brows.Count;side++)
                {
                    var b=brows[side] as JArray;var line=browLines!=null&&side<browLines.Count?browLines[side] as JObject:null;if(b==null||line==null)continue;
                    double w=BrowWeight(b,line,x,y);if(w>layer.wBrow[i]){layer.wBrow[i]=(float)w;layer.browSide[i]=(sbyte)side;layer.browCap[i]=line["cap"] is JArray cap?(float)Interp((JArray)line["x"],cap,x):float.PositiveInfinity;}
                }
            }
            layer.skin=SkinOf(def["bones"] as JArray,layer.rest);
            if(layer.skin!=null&&!((JArray)def["bones"]).All(id=>chainDefs.FirstOrDefault(d=>(string)d["id"]==(string)id)?["reach"]!=null)){Array.Clear(layer.wPhys1,0,n);Array.Clear(layer.wPhys2,0,n);}
            if(def["rigid"] is JArray){Array.Clear(layer.wPhys1,0,n);Array.Clear(layer.wPhys2,0,n);}
            layer.altSkin=layer.alt!=null&&bangs!=null?SkinOf(bangs["bones"] as JArray,layer.rest):null;
            if(layer.altSkin!=null)for(int i=0;i<n;i++){layer.alt[5*i+2]=0;layer.alt[5*i+3]=0;}
            return layer;
        }
        public static double Interp(JArray xs,JArray ys,double x)
        {if(x<=(double)xs[0])return (double)ys[0];for(int i=1;i<xs.Count;i++)if(x<=(double)xs[i])return (double)ys[i-1]+((double)ys[i]-(double)ys[i-1])*(x-(double)xs[i-1])/((double)xs[i]-(double)xs[i-1]);return (double)ys[ys.Count-1];}
        public static double BrowWeight(JArray b,JObject line,double x,double y)
        {
            double wx=1-Smooth(0,((double)b[2]-(double)b[0])*.45,Math.Max(0,Math.Max((double)b[0]-x,x-(double)b[2])));
            if(y<(double)b[1])return wx*(1-Smooth(0,((double)b[3]-(double)b[1])*1.6+6,(double)b[1]-y));
            double bot=Interp((JArray)line["x"],(JArray)line["bottom"],x);return y<=bot?wx:wx*(1-Smooth(bot,Interp((JArray)line["x"],(JArray)line["lid"],x),y));
        }
        public PuppetSkin SkinOf(JArray ids,float[] rest)
        {
            if(ids==null)return null;var cs=ids.Select(id=>Array.FindIndex(chainDefs,d=>(string)d["id"]==(string)id)).Where(c=>c>=0).ToArray();if(cs.Length==0)return null;
            int n=rest.Length/2,B=cs.Length;var s=new PuppetSkin{B=B,c=new short[n*B],seg=new short[n*B],t=new float[n*B],ux=new float[n*B],uy=new float[n*B],w=new float[n*B],sum=new float[n]};
            for(int i=0;i<n;i++)
            {
                double x=rest[2*i],y=rest[2*i+1],sum=0;
                for(int b=0;b<B;b++)
                {
                    int ci=cs[b];var r=chainRest[ci];int m=r.Length/2;double best=double.PositiveInfinity,bt=0;int bk=0;
                    for(int k=0;k<m-1;k++)
                    {
                        double ax=r[2*k],ay=r[2*k+1],dx=(double)r[2*k+2]-ax,dy=(double)r[2*k+3]-ay,l2=dx*dx+dy*dy;if(l2==0)l2=1;
                        double t=((x-ax)*dx+(y-ay)*dy)/l2;if(k>0)t=Math.Max(0,t);if(k<m-2)t=Math.Min(1,t);
                        double d=Hypot(x-ax-dx*t,y-ay-dy*t);if(d<best){best=d;bk=k;bt=t;}
                    }
                    int j=i*B+b;double tt=Math.Min(1,bt),px=r[2*bk]+((double)r[2*bk+2]-r[2*bk])*tt,py=r[2*bk+1]+((double)r[2*bk+3]-r[2*bk+1])*tt,sg=N(chainDefs[ci],"sigma",40);
                    s.c[j]=(short)ci;s.seg[j]=(short)bk;s.t[j]=(float)bt;s.ux[j]=(float)(x-px);s.uy[j]=(float)(y-py);s.w[j]=(float)(Math.Exp(-Math.Pow(best/sg,2))+1e-6);sum+=s.w[j];
                }
                double near=double.PositiveInfinity,reach=double.PositiveInfinity;
                for(int b=0;b<B;b++){int j=i*B+b;s.w[j]=(float)(s.w[j]/sum);reach=Math.Min(reach,N(chainDefs[s.c[j]],"reach",double.PositiveInfinity));near=Math.Min(near,Hypot(s.ux[j],s.uy[j]));}
                if(reach<double.PositiveInfinity){double k=1-Smooth(reach,1.6*reach,near);for(int b=0;b<B;b++)s.w[i*B+b]=(float)(s.w[i*B+b]*k);}
                for(int b=0;b<B;b++)s.sum[i]=(float)((double)s.sum[i]+s.w[i*B+b]);
            }
            return s;
        }
    }
}

namespace LivLiv.Runtime
{
    public sealed partial class Puppet
    {
        public Params @params=new Params();
        public readonly List<PuppetLayer> layers=new List<PuppetLayer>();
        public readonly Dictionary<string,PuppetSpring[]> chains=new Dictionary<string,PuppetSpring[]> {
            ["front"]=new[]{new PuppetSpring(),new PuppetSpring()},["back"]=new[]{new PuppetSpring(),new PuppetSpring()},["side"]=new[]{new PuppetSpring(),new PuppetSpring()} };
        public PuppetChain[] chainRuns=Array.Empty<PuppetChain>();
        public PuppetHead lastHead=new PuppetHead();
        public PuppetDrive drive=new PuppetDrive();
        public PuppetBody body=new PuppetBody();
        public PuppetBust bust=new PuppetBust();
        public double time,physicsScale=1,smileShown,mouthHeld=1,talkLeft;
        public int mouthFrame,mouthAmp;
        public string mouthShape="";
        public double[] mouthW={1,0,0};
        public readonly Dictionary<string,int> vis=new Dictionary<string,int>();
        public readonly Dictionary<string,double> expr=new Dictionary<string,double>();
        public readonly HashSet<string> hidden=new HashSet<string>();
        public bool bodyHidden,headHidden;
        private sealed class HeadTrigCache
        {
            public double ax=double.NaN,ay=double.NaN,az=double.NaN,t=double.NaN,n=double.NaN,ry=double.NaN,rp=double.NaN,rr=double.NaN,pe=double.NaN;
            public double cy=1,sy,cp=1,sp,cr=1,sr;
        }
        private readonly HeadTrigCache hxC=new HeadTrigCache();
        private HeadTrigCache HeadTrig(Params p)
        {
            var c=hxC;double tt=N(tuning,"turn"),nd=N(tuning,"nod",1),pe=N(rig,"pitch_ease",.25),ry=N(rig,"yaw"),rp=N(rig,"pitch"),rr=N(rig,"roll");
            if(c.ax!=p.angleX||c.ay!=p.angleY||c.az!=p.angleZ||c.t!=tt||c.n!=nd||c.ry!=ry||c.rp!=rp||c.rr!=rr||c.pe!=pe)
            {
                double py=p.angleY/30,yaw=p.angleX/30*ry*tt,pitch=py*(1-pe*py*py)*rp*tt*nd,roll=p.angleZ*Math.PI/180*rr*tt;
                c.ax=p.angleX;c.ay=p.angleY;c.az=p.angleZ;c.t=tt;c.n=nd;c.ry=ry;c.rp=rp;c.rr=rr;c.pe=pe;
                c.cy=Math.Cos(yaw);c.sy=Math.Sin(yaw);c.cp=Math.Cos(pitch);c.sp=Math.Sin(pitch);c.cr=Math.Cos(roll);c.sr=Math.Sin(roll);
            }
            return c;
        }
        // Values are scoped to one Deform call, never cached across mutable rig/anchor edits.
        // Keep source headXform/bodyOffset arithmetic in the shared overloads below.
        private struct HeadFrame
        {
            public double cx,cy,rx,ry,q,bulge,neck,driftX,driftY;
            public JArray extent;public HeadTrigCache trig;
        }
        private HeadFrame ReadHeadFrame(Params p)
        {
            var a=model["anchors"];var h=a["head"];var drift=rig["drift"] as JArray;
            return new HeadFrame{cx=N(h,"cx"),cy=N(h,"cy"),rx=N(h,"rx"),ry=N(h,"ry"),q=N(rig,"bulge_shape",1),
                bulge=N(rig,"bulge"),neck=N(a,"neck_y"),extent=rig["bulge_extent"] as JArray,
                driftX=drift!=null?(double)drift[0]:.12,driftY=drift!=null?(double)drift[1]:.08,trig=HeadTrig(p)};
        }
        public void HeadXform(double x,double y,double depth,Params p,double[] output)
        {
            var frame=ReadHeadFrame(p);HeadXform(x,y,depth,in frame,output);
        }
        private static void HeadXform(double x,double y,double depth,in HeadFrame frame,double[] output)
        {
            double cx=frame.cx,cy=frame.cy,rx=frame.rx,ry=frame.ry,u=(x-cx)/rx,v=(y-cy)/ry,q=frame.q;
            var e=frame.extent;double ex=e!=null?(double)e[0]:1;
            if(e!=null&&e.Count>3&&v>0){double t=Math.Min(1,v/((double)e[2]*(e.Count>4?(double)e[4]:1)));ex+=((double)e[3]-(double)e[0])*t*t*(3-2*t);}
            double bu=e!=null?u/ex:u,bv=e!=null?v/(double)e[v<0?1:2]:v;
            double bulge=frame.bulge*Math.Max(0,q==1?1-bu*bu-bv*bv:1-Math.Pow(bu*bu+bv*bv,q)),z=bulge+depth;
            var c=frame.trig;double u2=u*c.cy+z*c.sy,z2=-u*c.sy+z*c.cy,v2=v*c.cp-z2*c.sp;
            u2+=frame.driftX*c.sy;v2-=frame.driftY*c.sp;
            double X=cx+u2*rx,Y=cy+v2*ry,ny=frame.neck,dx=X-cx,dy=Y-ny;
            output[0]=cx+dx*c.cr-dy*c.sr;output[1]=ny+dx*c.sr+dy*c.cr;
        }
        private struct BodyFrame
        {
            public double cx,ry,bottom,neck,fw,br,yaw,roll,chestX,chestY,chestRX,chestRY,bustAmount,bustX,bustY;
            public bool chest;
        }
        private BodyFrame ReadBodyFrame(Params p,bool useBust=true)
        {
            var a=model["anchors"];var h=a["head"];var b=rig["body"];var chest=a["chest"];
            var frame=new BodyFrame{cx=N(h,"cx"),ry=N(h,"ry"),bottom=N(a,"bottom_y"),neck=N(a,"neck_y"),
                fw=(double)a["face"][2]-(double)a["face"][0],br=p.breath*N(tuning,"breath")*N(rig,"breath")*N(b,"breath"),
                yaw=body.yaw/30,roll=body.roll,chest=chest is JObject&&useBust,bustX=bust.x,bustY=bust.y};
            if(frame.chest){frame.chestX=N(chest,"cx");frame.chestY=N(chest,"cy");frame.chestRX=N(chest,"rx");frame.chestRY=N(chest,"ry");frame.bustAmount=N(b,"bust");}
            return frame;
        }
        public void BodyOffset(double x,double y,Params p,double[] output,bool useBust=true)
        {
            var frame=ReadBodyFrame(p,useBust);BodyOffset(x,y,in frame,output);
        }
        private static void BodyOffset(double x,double y,in BodyFrame frame,double[] output)
        {
            double cx=frame.cx,ry=frame.ry,bottom=frame.bottom,neck=frame.neck;
            double t=Smooth(bottom,neck,y),fw=frame.fw,br=frame.br;
            double dx=(x-cx)*.01*br*t,dy=-br*.024*ry*Math.Pow(t,1.3),yaw=frame.yaw,bw=1.4*fw,u=Math.Max(-1.6,Math.Min(1.6,(x-cx)/bw));
            dx+=yaw*bw*.16*Math.Pow(t,1.2)*(1-.18*u*u)+yaw*(x-cx)*-.04*Math.Sign(u)*t;
            double angle=frame.roll*Math.PI/180*t;
            if(angle!=0){double py=bottom+.4*(bottom-neck),rx=x-cx,rY=y-py,c=Math.Cos(angle),s=Math.Sin(angle);dx+=rx*(c-1)-rY*s;dy+=rx*s+rY*(c-1);}
            if(frame.chest)
            {
                double ux=(x-frame.chestX)/frame.chestRX,uy=(y-frame.chestY)/frame.chestRY,d=Math.Sqrt(ux*ux+uy*uy),w=1-Smooth(.2,1.15,d);
                if(w>0){dy-=br*.012*ry*w*(.4+frame.bustAmount);dx+=frame.bustX*w;dy+=frame.bustY*w;}
            }
            output[0]=dx;output[1]=dy;
        }
        public void StepBody(double dt)
        {
            var p=@params;var b=rig["body"];double tt=N(tuning,"turn"),ty=p.bodyX+p.angleX*N(b,"yaw")*tt,tr=p.bodyZ+p.angleZ*N(b,"roll")*tt,k=Math.Min(1,dt*5);
            body.yaw+=(ty-body.yaw)*k;body.roll+=(tr-body.roll)*k;
            var chest=model["anchors"]["chest"];if(!(chest is JObject)||N(b,"bust")<=0){bust.x=bust.y=0;return;}
            var o=new double[2];BodyOffset(N(chest,"cx"),N(chest,"cy"),p,o,false);
            double inv=1/Math.Max(dt,1e-4),ax=(o[0]-bust.px)*inv,ay=(o[1]-bust.py)*inv;bust.px=o[0];bust.py=o[1];
            double s=N(b,"bust")*physicsScale;
            bust.vx+=(-70*bust.x-7*bust.vx-ax*3.2*s)*dt;bust.vy+=(-70*bust.y-7*bust.vy-ay*4.2*s)*dt;bust.x+=bust.vx*dt;bust.y+=bust.vy*dt;
            double lim=.035*N(model["anchors"]["head"],"ry")*(.5+s);bust.x=Math.Max(-lim,Math.Min(lim,bust.x));bust.y=Math.Max(-lim,Math.Min(lim,bust.y));
        }
        public void ResetMouth(){mouthFrame=mouthAmp=0;mouthShape="";mouthHeld=1;mouthW=frameKind.Select((_,i)=>i==0?1d:0).ToArray();}
        public void StepMouth(double dt)
        {
            double m=Math.Min(1,Math.Max(0,@params.mouthOpen)),v=Math.Min(1,Math.Max(-1,double.IsNaN(@params.vowel)?0:@params.vowel));mouthHeld+=dt;
            int a=mouthAmp;if(a==0&&m>.12+.04)a=m>.5+.04?2:1;else if(a==1&&(m<.12-.04||m>.5+.04))a=m<.12-.04?0:2;else if(a==2&&m<.5-.04)a=m<.12-.04?0:1;
            string s=mouthShape;if(s.Length==0||(s=="round"?v>-.35:v<.35))s=v<-.65?"round":v>.65?"wide":"";
            int want=a>0&&s.Length>0&&vis.TryGetValue(s,out var shaped)?shaped:a;
            bool hasShut=vis.TryGetValue("shut",out var shut);if(want==0&&talkLeft>0&&hasShut)want=shut;
            if(want==mouthFrame){mouthAmp=a;mouthShape=s;}else if(mouthHeld>=.055){mouthFrame=want;mouthAmp=a;mouthShape=s;mouthHeld=0;}
            int f=mouthFrame;talkLeft=f>0&&(!hasShut||f!=shut)?.4:Math.Max(0,talkLeft-dt);
            double k=Math.Min(1,dt*28);for(int i=0;i<mouthW.Length;i++)mouthW[i]+=((i==f?1:0)-mouthW[i])*k;
        }
        public double FrameEase(int i,double m){string kind=i>=0&&i<frameKind.Length?frameKind[i]:"";return kind=="half"?Smooth(.12,.5,m):kind=="open"?Smooth(.5,1,m):Smooth(.12,1,m);}
        public double ExprValue(string name)=>name=="smile"?smileShown:expr.TryGetValue(name,out var value)?value:0;
        public double ExprAlpha(JObject def){string p=(string)def["param"];return Smooth(.3,.7,string.IsNullOrEmpty(p)||p=="smile"?smileShown:ExprValue(p));}
        public double MoodAlpha(JObject def)
        {
            double Sum(JToken weights){double a=0;if(weights is JObject obj)foreach(var v in obj.Properties())a+=(double)v.Value*ExprValue(v.Name);return Math.Min(1,a);}
            return (def["show"] is JObject?Sum(def["show"]):1)*(def["hide"] is JObject?1-Sum(def["hide"]):1);
        }
        public bool Shown(PuppetLayer layer)
        {
            var d=layer.def;string role=(string)d["role"]??"";
            if(hidden.Contains((string)d["id"])||((string)d["group"]=="body"?bodyHidden:headHidden))return false;
            if((d["show"] is JObject||d["hide"] is JObject)&&MoodAlpha(d)<=.01)return false;
            if(role=="expr"||role=="expr_brows")return ExprAlpha(d)>.01;
            if(role.StartsWith("lid_",StringComparison.Ordinal))return 1-Smooth(.05,.35,role=="lid_L"?@params.eyeLOpen:@params.eyeROpen)>.01;
            if(role=="blush"){double a=0;if(d["blush"] is JObject blush)foreach(var p in blush.Properties())a+=(double)p.Value*ExprValue(p.Name);return a>.01;}
            return true;
        }
        public void StepPhysics(double dt)
        {
            var p=@params;var a=model["anchors"];var h=a["head"];var ht=new double[2];HeadXform(N(h,"cx"),N(a,"hair_root_y"),0,p,ht);
            double inv=1/Math.Max(dt,1e-4),vx=(ht[0]-lastHead.x)*inv,vy=(ht[1]-lastHead.y)*inv;lastHead=new PuppetHead{x=ht[0],y=ht[1],roll=p.angleZ};
            var d=rig["hair_drive"] as JArray;var dv=drive;
            if(d!=null)
            {
                var fp=new double[2];HeadXform(N(h,"cx"),N(h,"cy"),0,p,fp);
                if(!dv.started){dv.x=fp[0];dv.y=fp[1];dv.vx=dv.vy=dv.ax=dv.ay=0;dv.started=true;}
                double ks=Math.Min(1,dt*25),svx=dv.vx+((fp[0]-dv.x)*inv-dv.vx)*ks,svy=dv.vy+((fp[1]-dv.y)*inv-dv.vy)*ks;
                dv.ax+=((svx-dv.vx)*inv-dv.ax)*ks;dv.ay+=((svy-dv.vy)*inv-dv.ay)*ks;dv.x=fp[0];dv.y=fp[1];dv.vx=svx;dv.vy=svy;
            }
            double lim=N(h,"ry")*N(rig,"hair_limit");
            foreach(string key in rig["hair"]["side"] is JArray?new[]{"front","back","side"}:new[]{"front","back"})
            {
                var cfg=(JArray)rig["hair"][key];double k1=(double)cfg[0],c1=(double)cfg[1],gain=(double)cfg[2],k2=(double)cfg[3],c2=(double)cfg[4];var aa=chains[key][0];var b=chains[key][1];
                double s=physicsScale*N(tuning,key=="back"?"hairBack":"hairFront",N(tuning,"hair"));
                double wind=key=="side"?Math.Sin(time*1.5+.7)*1.2*s*N(rig,"wind"):Math.Sin(time*(key=="front"?1.9:1.15))*(key=="front"?.6:1.8)*s*N(rig,"wind"),gravity=-p.angleZ*.2*s;
                double ax=-k1*(aa.x-wind-gravity)-c1*aa.vx-vx*gain*k1*.06*s,ay=-k1*aa.y-c1*aa.vy-vy*gain*k1*.04*s;
                if(d!=null){double m=(double)d[key=="front"?0:key=="back"?1:d.Count>2?2:0]*s;ax-=dv.ax*m;ay-=dv.ay*m;}
                aa.vx+=ax*dt;aa.vy+=ay*dt;aa.x+=aa.vx*dt;aa.y+=aa.vy*dt;
                double bx=-k2*(b.x-aa.x*1.6)-c2*b.vx,by=-k2*(b.y-aa.y*1.4)-c2*b.vy;b.vx+=bx*dt;b.vy+=by*dt;b.x+=b.vx*dt;b.y+=b.vy*dt;
                foreach(var q in chains[key]){q.x=Math.Max(-lim,Math.Min(lim,q.x));q.y=Math.Max(-lim*.6,Math.Min(lim*.6,q.y));}
            }
        }
        public static PuppetChain ChainRun(JObject def)
        {
            var r=((JArray)def["points"]).SelectMany(p=>((JArray)p).Select(v=>(float)(double)v)).ToArray();int m=r.Length/2;var len=new float[m];var s=new float[m];
            for(int j=1;j<m;j++){len[j]=(float)Hypot((double)r[2*j]-r[2*j-2],(double)r[2*j+1]-r[2*j-1]);s[j]=(float)((double)s[j-1]+len[j]);}
            double total=s[m-1]!=0?s[m-1]:1;for(int j=1;j<m;j++)s[j]=(float)(s[j]/total);
            int hash=0;string id=(string)def["id"];for(int i=0;i<id.Length;i++){hash=(hash*31+id[i])%997;if(char.IsHighSurrogate(id[i])&&i+1<id.Length&&char.IsLowSurrogate(id[i+1]))i++;}
            return new PuppetChain{def=def,R=r,p=(float[])r.Clone(),q=(float[])r.Clone(),len=len,s=s,disp=new float[m*2],beta=new float[m],phase=hash/997d*2*Math.PI};
        }
        public void ResetPhysics()
        {
            foreach(var c in chainRuns){Array.Copy(c.R,c.p,c.R.Length);Array.Copy(c.R,c.q,c.R.Length);Array.Clear(c.disp,0,c.disp.Length);Array.Clear(c.beta,0,c.beta.Length);c.started=c.moving=false;}
            foreach(var chain in chains.Values)foreach(var q in chain)q.x=q.y=q.vx=q.vy=0;
            lastHead=new PuppetHead();drive.started=false;body=new PuppetBody();bust=new PuppetBust();
        }
        public void ChainRoot(JObject def,Params p,double[] nb,double[] output)
        {
            double rx=(double)def["points"][0][0],ry=(double)def["points"][0][1];var a=model["anchors"];var point=new double[2];
            if((string)def["attach"]=="body"){BodyOffset(rx,ry,p,point);output[0]=rx+point[0];output[1]=ry+point[1];output[2]=body.roll*Math.PI/180*Smooth(N(a,"bottom_y"),N(a,"neck_y"),ry);}
            else {HeadXform(rx,ry,N(def,"depth",0),p,point);output[0]=point[0]+nb[0];output[1]=point[1]+nb[1];output[2]=p.angleZ*Math.PI/180*N(rig,"roll")*N(tuning,"turn");}
        }
        public void StepChains(double dt)
        {
            if(chainRuns.Length==0||dt<=0)return;var p=@params;var a=model["anchors"];var nb=new double[2];BodyOffset(N(a["head"],"cx"),N(a,"neck_y"),p,nb);
            double g=physicsScale*N(tuning,"hairBack",N(tuning,"hair"));var root=new double[3];
            foreach(var c in chainRuns)
            {
                var d=c.def;var r=c.R;var pos=c.p;var prev=c.q;int m=r.Length/2;ChainRoot(d,p,nb,root);
                if(!c.started){c.W=new[]{root[0],root[1]};c.V=new double[2];c.acc=new double[2];c.started=true;}
                double vx=(root[0]-c.W[0])/dt,vy=(root[1]-c.W[1])/dt,ax=(vx-c.V[0])/dt,ay=(vy-c.V[1])/dt;c.W[0]=root[0];c.W[1]=root[1];c.V[0]=vx;c.V[1]=vy;c.rho=root[2];
                double ks=Math.Min(1,dt*30);c.acc[0]+=(ax-c.acc[0])*ks;c.acc[1]+=(ay-c.acc[1])*ks;
                double cr=Math.Cos(-c.rho),sr=Math.Sin(-c.rho),lax=c.acc[0]*cr-c.acc[1]*sr,lay=c.acc[0]*sr+c.acc[1]*cr,bias=0;
                if(d["bias"] is JObject weights)foreach(var b in weights.Properties())bias+=(double)b.Value*ExprValue(b.Name);
                double turn=-c.rho*N(d,"hang")+bias*Math.PI/180,ct=Math.Cos(turn),st=Math.Sin(turn),f=N(d,"freq",.5),wt=2*Math.PI*f*time+c.phase;
                double wind=N(d,"wind")*g*N(rig,"wind")*(Math.Sin(wt)+.4*Math.Sin(1.7*wt+2*c.phase)),damp=Math.Max(0,1-N(d,"damp")*dt);
                for(int j=1;j<m;j++)
                {
                    double s=c.s[j],k=(double)d["k"][0]+((double)d["k"][1]-(double)d["k"][0])*s,tx=(double)r[2*j]-r[0],ty=(double)r[2*j+1]-r[1];
                    double gx=r[0]+tx*ct-ty*st+wind*s,gy=r[1]+tx*st+ty*ct,fx=k*(gx-pos[2*j])-N(d,"drive")*g*lax,fy=k*(gy-pos[2*j+1])-N(d,"drive")*g*lay;
                    double nx=pos[2*j]+((double)pos[2*j]-prev[2*j])*damp+fx*dt*dt,ny=pos[2*j+1]+((double)pos[2*j+1]-prev[2*j+1])*damp+fy*dt*dt;
                    prev[2*j]=pos[2*j];prev[2*j+1]=pos[2*j+1];pos[2*j]=(float)nx;pos[2*j+1]=(float)ny;
                }
                double limit=N(d,"limit",double.PositiveInfinity);
                for(int j=1;j<m;j++)
                {
                    double dx=(double)pos[2*j]-pos[2*j-2],dy=(double)pos[2*j+1]-pos[2*j-1],l=Hypot(dx,dy);if(l==0)l=1;
                    pos[2*j]=(float)(pos[2*j-2]+dx/l*c.len[j]);pos[2*j+1]=(float)(pos[2*j-1]+dy/l*c.len[j]);
                    double ex=(double)pos[2*j]-r[2*j],ey=(double)pos[2*j+1]-r[2*j+1],e=Hypot(ex,ey);
                    if(e>limit){pos[2*j]=(float)(r[2*j]+ex*limit/e);pos[2*j+1]=(float)(r[2*j+1]+ey*limit/e);}
                }
                double mv=0;for(int j=0;j<m;j++){c.disp[2*j]=(float)((double)pos[2*j]-r[2*j]);c.disp[2*j+1]=(float)((double)pos[2*j+1]-r[2*j+1]);mv=Math.Max(mv,Math.Max(Math.Abs(c.disp[2*j]),Math.Abs(c.disp[2*j+1])));}c.moving=mv>1e-3;
                var al=new double[m-1];for(int k=0;k<m-1;k++){double rx=(double)r[2*k+2]-r[2*k],ry=(double)r[2*k+3]-r[2*k+1],cx=(double)pos[2*k+2]-pos[2*k],cy=(double)pos[2*k+3]-pos[2*k+1];al[k]=Math.Atan2(rx*cy-ry*cx,rx*cx+ry*cy);}
                c.beta[0]=0;for(int j=1;j<m-1;j++)c.beta[j]=(float)(.5*(al[j-1]+al[j]));c.beta[m-1]=(float)al[m-2];
            }
        }
        public void SkinOffset(PuppetSkin skin,int i,double[] output)
        {
            double ox=0,oy=0;if(skin.sum[i]<1e-4){output[0]=output[1]=0;return;}
            for(int b=0;b<skin.B;b++)
            {
                int j=i*skin.B+b;var c=chainRuns[skin.c[j]];int k=skin.seg[j];if(!c.moving)continue;
                double t=Math.Min(1,Math.Max(0,skin.t[j])),e=skin.t[j]<0?0:1;
                double dx=(c.disp[2*k]+((double)c.disp[2*k+2]-c.disp[2*k])*t)*e,dy=(c.disp[2*k+1]+((double)c.disp[2*k+3]-c.disp[2*k+1])*t)*e,be=(c.beta[k]+((double)c.beta[k+1]-c.beta[k])*t)*e;
                double cos=Math.Cos(be),sin=Math.Sin(be),ux=skin.ux[j],uy=skin.uy[j],lx=dx+ux*cos-uy*sin-ux,ly=dy+ux*sin+uy*cos-uy,cr=Math.Cos(c.rho),sr=Math.Sin(c.rho);
                ox+=skin.w[j]*(lx*cr-ly*sr);oy+=skin.w[j]*(lx*sr+ly*cr);
            }
            output[0]=ox;output[1]=oy;
        }
        public static double Twinkle(double t){double k=Math.Floor(t/5.3),u=t-k*5.3-((k*7919)%13)/13*3.5,spark=u>0&&u<.35?Math.Sin(u/.35*Math.PI):0;return 1+.03*Math.Sin(t*2.1)+.12*spark;}
        // Source update(..., mesh=false): advance physics even when the host skips drawing this view.
        public void UpdatePhysics(double dt)
        {
            if(model==null)return;time+=dt;smileShown+=(@params.smile-smileShown)*Math.Min(1,dt*7);StepMouth(dt);
            foreach(var k in new[]{"angleX","angleY","angleZ"})@params[k]=Math.Max(Params.Ranges[k][0],Math.Min(Params.Ranges[k][1],@params[k]));
            int steps=Math.Max(1,(int)Math.Ceiling(dt/(1d/120)));for(int i=0;i<steps;i++){StepBody(dt/steps);StepPhysics(dt/steps);StepChains(dt/steps);}
        }
    }
}

namespace LivLiv.Runtime
{
    public sealed class PuppetNasal { public double room,sign,inner,w; }
    public sealed class PuppetKey { public string param;public double value,scale; }
    public sealed partial class Puppet
    {
        public bool irisLid=true,irisNasal=true,kfOff;
        public double irisDrop=.3,irisNasalNarrow=.7;
        public readonly Dictionary<string,PuppetNasal> nasal=new Dictionary<string,PuppetNasal>();
        public readonly List<PuppetKey> keys=new List<PuppetKey>();
        public double[] keyWeights;
        public int[] kfPair;
        public bool chained=>chainRuns.Length>0;
        public static double[] BrowLimits(JToken anchors,double amp)
        {
            double up=amp,down=amp;bool any=false;
            var brows=anchors["brows"] as JArray;var lines=anchors["brow_lines"] as JArray;
            for(int side=0;side<(brows?.Count??0);side++)
            {
                if(!(brows[side] is JArray b)||lines==null||side>=lines.Count||!(lines[side] is JObject line))continue;
                any=true;up=Math.Min(up,.6*(((double)b[3]-(double)b[1])*1.6+6));
                for(int k=0;k<((JArray)line["x"]).Count;k++)
                {
                    double x=(double)line["x"][k],wx=1-Smooth(0,((double)b[2]-(double)b[0])*.45,Math.Max(Math.Max((double)b[0]-x,x-(double)b[2]),0));
                    down=Math.Min(down,.6*((double)line["lid"][k]-(double)line["bottom"][k])/Math.Max(1e-3,wx));
                }
            }
            return any?new[]{up,down}:new double[2];
        }
        // Numeric part of update(): GPU upload belongs to the platform adapter.
        public void Update(double dt,bool mesh=true){UpdatePhysics(dt);if(model!=null&&mesh)Deform();}
        public void Deform()
        {
            var p=@params;var a=model["anchors"];var head=a["head"];double cx=N(head,"cx"),rx=N(head,"rx"),neck=N(a,"neck_y");
            var headFrame=ReadHeadFrame(p);var bodyFrame=ReadBodyFrame(p);
            var nb=new double[2];var hp=new double[2];var bp=new double[2];var pt=new double[2];var so=new double[2];BodyOffset(cx,neck,in bodyFrame,nb);
            var limits=BrowLimits(a,N(head,"ry")*N(rig,"brow"));
            double Lift(double v)=>v>0?Math.Min(1,v)*limits[0]:Math.Max(-1,v)*limits[1];
            var browLift=new[]{Lift(p.browLY),Lift(p.browRY)};double mo=Math.Min(1,Math.Max(0,p.mouthOpen)),jawDrop=0;
            for(int i=0;i<mouthW.Length;i++)if(mouthW[i]>0&&frameJaw[i]>0)jawDrop+=mouthW[i]*frameJaw[i]*(frameKind[i]=="half"?.6+.4*FrameEase(i,mo):.75+.25*FrameEase(i,mo));
            bool sideOn=rig["hair"]["side"] is JArray;
            void Place(double x,double y,double wh,double dep,double w1,double w2,double f)
            {
                if(wh<1)BodyOffset(x,y,in bodyFrame,bp);else bp[0]=bp[1]=0;
                double X=x+bp[0],Y=y+bp[1];
                if(wh>0){HeadXform(x,y,dep,in headFrame,hp);X=X*(1-wh)+(hp[0]+nb[0])*wh;Y=Y*(1-wh)+(hp[1]+nb[1])*wh;}
                if(w1>0||w2>0)
                {
                    var b=chains["back"];var front=chains["front"];double c0x,c0y,c1x,c1y;
                    if(sideOn)
                    {
                        var side=chains["side"];double sd=Smooth(.5,.85,Math.Abs(x-cx)/rx),ff=f*(1-sd),fs=f*sd;
                        c0x=front[0].x*ff+side[0].x*fs+b[0].x*(1-f);c0y=front[0].y*ff+side[0].y*fs+b[0].y*(1-f);
                        c1x=front[1].x*ff+side[1].x*fs+b[1].x*(1-f);c1y=front[1].y*ff+side[1].y*fs+b[1].y*(1-f);
                    }
                    else{c0x=front[0].x*f+b[0].x*(1-f);c0y=front[0].y*f+b[0].y*(1-f);c1x=front[1].x*f+b[1].x*(1-f);c1y=front[1].y*f+b[1].y*(1-f);}
                    X+=c0x*w1+c1x*w2;Y+=c0y*w1+c1y*w2;
                }
                pt[0]=X;pt[1]=Y;
            }
            var kw=kfOff?Array.Empty<double>():keyWeights??keys.Select(k=>k.param=="diag"?0:Math.Min(2,Math.Max(0,p[k.param]*k.scale/k.value))).ToArray();
            if(kfPair!=null&&!kfOff&&keyWeights==null)
            {
                var u=(double[])kw.Clone();for(int k=0;k<u.Length;k++){int j=kfPair[k];if(j<0||u[k]<=0||u[k]>=1)continue;double t=u[k],h10=t*t*t-2*t*t+t;kw[k]=3*t*t-2*t*t*t+.5*h10+t*t*t-t*t;kw[j]-=.5*h10;}
            }
            double roll=p.angleZ*Math.PI/180*N(rig,"roll")*N(tuning,"turn"),g=N(tuning,"gaze"),gz=g>=0&&g<1?g:1,chinDx=0;
            if(rig["neck"] is JObject){double cy=N(a["jaw"],"chin",neck);HeadXform(cx,cy,0,in headFrame,hp);BodyOffset(cx,cy,in bodyFrame,bp);chinDx=hp[0]+nb[0]-(cx+bp[0]);}
            foreach(var layer in layers)
            {
                if(!kfOff&&!Shown(layer))continue;
                var d=layer.def;var r=layer.rest;var o=layer.output;int n=r.Length/2;string role=(string)d["role"]??"",eyeSide=role.EndsWith("_L",StringComparison.Ordinal)?"L":role.EndsWith("_R",StringComparison.Ordinal)?"R":"";
                bool iris=role.StartsWith("iris_",StringComparison.Ordinal),shine=role.StartsWith("shine_",StringComparison.Ordinal),isEye=role.StartsWith("eye_",StringComparison.Ordinal)||iris||shine;
                double open=eyeSide=="L"?p.eyeLOpen:eyeSide=="R"?p.eyeROpen:1,squash=.12+.88*open,ix=0,iy=0;
                var range=d["range"] as JArray;var pivot=d["pivot"] as JArray;
                if((iris||chained&&shine)&&range!=null){ix=p.eyeBallX*(double)range[0]*gz;iy=-p.eyeBallY*(double)range[1]*gz;}
                double nx=ix,nk=0;
                if(irisNasal&&ix!=0&&nasal.TryGetValue(eyeSide,out var nz)&&ix*nz.sign>nz.room){double far=Math.Min(ix*nz.sign,nz.room+(1-irisNasalNarrow)*nz.w);nk=-(far-nz.room)/nz.w;nx=nz.sign*nz.room-nk*nz.inner;}
                bool lidIris=iris&&irisLid&&pivot!=null&&range!=null&&((double)range[0]>0||(double)range[1]>0);
                if(lidIris)iy+=irisDrop*(1-squash)*((layer.pivC??(double)pivot[1])-(N(d,"y")+N(d,"h")/2));
                if(iris)layer.soft=Math.Min(1,Hypot(ix,iy)+(lidIris?(1-squash)*N(d,"h")/2:0));
                double tw=chained&&shine?Twinkle(time+(eyeSide=="R"?1.3:0)):1,tcx=N(d,"x")+N(d,"w")/2,tcy=N(d,"y")+N(d,"h")/2,rgx=0,rgy=0;
                var rigid=d["rigid"] as JArray;
                if(rigid!=null){HeadXform((double)rigid[0],(double)rigid[1],N(d,"depth"),in headFrame,hp);rgx=hp[0]+nb[0];rgy=hp[1]+nb[1];}
                double rc=Math.Cos(roll),rs=Math.Sin(roll);
                for(int i=0;i<n;i++)
                {
                    double x=r[2*i],y=r[2*i+1];
                    if(tw!=1){x=tcx+(x-tcx)*tw;y=tcy+(y-tcy)*tw;}
                    if(isEye&&pivot!=null){double py=layer.pivY!=null?layer.pivY[i]:(double)pivot[1];x+=nx+nk*r[2*i];y+=iy;if(!lidIris||y>py)y=py+(y-py)*squash;}
                    if(layer.browSide[i]>=0){double v=browLift[layer.browSide[i]];y-=Math.Sign(v)*Math.Min(Math.Abs(v),layer.browCap[i])*layer.wBrow[i];}
                    if(jawDrop>0&&layer.wJaw[i]>0)y+=jawDrop*layer.wJaw[i];
                    if(rigid!=null){double vx=x-(double)rigid[0],vy=y-(double)rigid[1];pt[0]=rgx+vx*rc-vy*rs;pt[1]=rgy+vx*rs+vy*rc;}
                    else Place(x,y,layer.wHead[i],layer.dep[i],layer.wPhys1[i],layer.wPhys2[i],layer.wFront[i]);
                    if(layer.skin!=null){SkinOffset(layer.skin,i,so);pt[0]+=so[0];pt[1]+=so[1];}
                    double X=pt[0],Y=pt[1];
                    if(layer.wTwist!=null&&layer.wTwist[i]>0)X+=chinDx*layer.wTwist[i];
                    if(layer.wNeck!=null&&layer.wNeck[i]>0){double angle=roll*layer.wNeck[i],c=Math.Cos(angle),s=Math.Sin(angle),px=cx+nb[0],py=neck+nb[1],dx=X-px,dy=Y-py;X=px+dx*c-dy*s;Y=py+dx*s+dy*c;}
                    double cover=layer.fol!=null?layer.fol[i]:0,coverB=layer.folB!=null?layer.folB[i]:0;
                    if(cover>0||coverB>0)
                    {
                        int k=5*i;var alt=layer.alt;double X0=X,Y0=Y;Place(x,y,alt[k+1],alt[k],alt[k+2],alt[k+3],alt[k+4]);
                        if(layer.altSkin!=null){SkinOffset(layer.altSkin,i,so);pt[0]+=so[0];pt[1]+=so[1];}
                        X+=(pt[0]-X0)*cover;Y+=(pt[1]-Y0)*cover;
                        if(layer.outB!=null){layer.outB[2*i]=(float)(X0+(pt[0]-X0)*coverB);layer.outB[2*i+1]=(float)(Y0+(pt[1]-Y0)*coverB);}
                    }
                    else if(layer.outB!=null){layer.outB[2*i]=(float)X;layer.outB[2*i+1]=(float)Y;}
                    if(layer.kf!=null)
                    {
                        double dx=0,dy=0;for(int k=0;k<kw.Length;k++){var field=layer.kf[k];if(field!=null&&kw[k]!=0){dx+=kw[k]*field[2*i];dy+=kw[k]*field[2*i+1];}}
                        if(dx!=0||dy!=0){double angle=roll*layer.wHead[i],c=Math.Cos(angle),s=Math.Sin(angle),ox=dx*c-dy*s,oy=dx*s+dy*c;X+=ox;Y+=oy;if(layer.outB!=null){layer.outB[2*i]=(float)(layer.outB[2*i]+ox);layer.outB[2*i+1]=(float)(layer.outB[2*i+1]+oy);}}
                    }
                    o[2*i]=(float)X;o[2*i+1]=(float)Y;
                }
            }
        }
    }
}

namespace LivLiv.Runtime
{
    public sealed partial class Puppet
    {
        public static bool LidCurveFits(JObject m)
        {
            var profile=m["profile"];var input=profile?["class"]?["input"];string id=(string)profile?["id"];
            return (id=="anime_half"||id=="painterly")&&(string)input?["facing"]=="front"&&!(input?["glasses"]?.Type==JTokenType.Boolean&&(bool)input["glasses"])&&!((JArray)m["layers"]).Any(l=>(string)l["id"]=="glasses");
        }
        // coverAt's Canvas drawImage boundary is supplied by the platform; these are model-sized alpha readbacks.
        public static float[] CoverAt(byte[] own,byte[] under,JObject def,float[] points,int cell)
        {
            int width=(int)N(def,"w"),height=(int)N(def,"h");if(own.Length!=width*height||under.Length!=own.Length)throw new ArgumentException("Coverage dimensions differ");
            var output=new float[points.Length/2];
            for(int i=0;i<output.Length;i++)
            {
                double cx=points[2*i]-N(def,"x"),cy=points[2*i+1]-N(def,"y"),sa=0,sab=0,sb=0;int count=0;
                for(int y=Math.Max(0,(int)Math.Floor(cy-cell+.5));y<Math.Min(height,Math.Floor(cy+cell+.5));y++)
                for(int x=Math.Max(0,(int)Math.Floor(cx-cell+.5));x<Math.Min(width,Math.Floor(cx+cell+.5));x++)
                {int k=y*width+x;double a=own[k]/255d,b=under[k]/255d;sa+=a;sab+=a*b;sb+=b;count++;}
                output[i]=(float)(sa>1?sab/sa:count>0?sb/count:0);
            }
            return output;
        }
        public static PuppetNasal NasalRoom(byte[] iris,int width,int height,JObject def,byte[] mask,int maskWidth,int maskHeight,JObject eye,double sign)
        {
            double ux=N(def,"w")/width,uy=N(def,"h")/height;
            bool White(double x,double y){int u=(int)Math.Floor((x-N(eye,"x"))/N(eye,"w")*maskWidth),v=(int)Math.Floor((y-N(eye,"y"))/N(eye,"h")*maskHeight);return u>=0&&v>=0&&u<maskWidth&&v<maskHeight&&mask[v*maskWidth+u]>=128;}
            var rows=new List<double[]>();
            for(int v=0;v<height;v++)
            {
                double y=N(def,"y")+(v+.5)*uy,lo=double.PositiveInfinity,hi=double.NegativeInfinity;int n=0;
                for(int u=0;u<width;u++){double x=N(def,"x")+(u+.5)*ux;if(iris[v*width+u]<200||!White(x,y))continue;n++;lo=Math.Min(lo,x);hi=Math.Max(hi,x);}
                if(n>0)rows.Add(new[]{y,(double)n,sign>0?hi:lo,sign>0?lo:hi});
            }
            if(rows.Count==0)return null;double top=rows.Max(r=>r[1]);var room=new List<double>();var inner=new List<double>();var widths=new List<double>();
            foreach(var r in rows){if(r[1]<.9*top)continue;int k=0;while(White(r[2]+sign*(k+1)*ux,r[0]))k++;room.Add(k*ux);inner.Add(r[2]+sign*ux/2);widths.Add(Math.Abs(r[2]-r[3])+ux);}
            double Median(List<double> values){values.Sort();return values[values.Count>>1];}
            return new PuppetNasal{room=Median(room),sign=sign,inner=Median(inner),w=Median(widths)};
        }
        public static Func<double,double> ClosedLashLine(byte[] rgba,int width,int height,JObject def)
        {
            if(rgba.Length!=(long)width*height*4)throw new ArgumentException("Lash dimensions differ");
            var points=new List<double[]>();
            for(int x=0;x<width;x++)
            {
                double best=255;int by=-1;
                for(int y=0;y<height;y++){int k=4*(y*width+x);if(rgba[k+3]<200)continue;double l=.3*rgba[k]+.59*rgba[k+1]+.11*rgba[k+2];if(l<best){best=l;by=y;}}
                if(by>=0&&best<110)points.Add(new[]{N(def,"x")+(x+.5)*N(def,"w")/width,N(def,"y")+(by+.5)*N(def,"h")/height});
            }
            if(points.Count<.2*width)return null;
            Func<double,double> Fit(List<double[]> pts)
            {
                double mid=pts.Sum(p=>p[0])/pts.Count,scale=Math.Max(1,(pts[pts.Count-1][0]-pts[0][0])/2);var sums=new double[5];var targets=new double[3];
                foreach(var p in pts){double u=(p[0]-mid)/scale,q=1;for(int k=0;k<5;k++){sums[k]+=q;if(k<3)targets[k]+=q*p[1];q*=u;}}
                var matrix=new[]{new[]{sums[0],sums[1],sums[2]},new[]{sums[1],sums[2],sums[3]},new[]{sums[2],sums[3],sums[4]}};
                double Det(double[][] a)=>a[0][0]*(a[1][1]*a[2][2]-a[1][2]*a[2][1])-a[0][1]*(a[1][0]*a[2][2]-a[1][2]*a[2][0])+a[0][2]*(a[1][0]*a[2][1]-a[1][1]*a[2][0]);
                double determinant=Det(matrix);if(!(Math.Abs(determinant)>1e-9))return null;
                var coefficients=new double[3];for(int j=0;j<3;j++){var replaced=matrix.Select((r,i)=>r.Select((v,k)=>k==j?targets[i]:v).ToArray()).ToArray();coefficients[j]=Det(replaced)/determinant;if(double.IsInfinity(coefficients[j])||double.IsNaN(coefficients[j]))return null;}
                return x=>{double u=(x-mid)/scale;return coefficients[0]+coefficients[1]*u+coefficients[2]*u*u;};
            }
            var fit=Fit(points);if(fit==null)return null;
            var residuals=points.Select(p=>Math.Abs(p[1]-fit(p[0]))).OrderBy(v=>v).ToArray();double tolerance=Math.Max(1.5,2.5*residuals[residuals.Length>>1]);
            var used=points;var kept=points.Where(p=>Math.Abs(p[1]-fit(p[0]))<=tolerance).ToList();var refit=kept.Count>=.6*points.Count?Fit(kept):null;
            if(refit!=null){fit=refit;used=kept;}
            double x0=used[0][0],x1=used[used.Count-1][0];return x=>fit(Math.Min(x1,Math.Max(x0,x)));
        }
    }
}

namespace LivLiv.Runtime
{
    public sealed partial class Puppet
    {
        public static bool KeyformsFit(JToken value)
        {
            if(!(value is JObject k))return false;
            bool Number(JToken t)=>t!=null&&(t.Type==JTokenType.Float||t.Type==JTokenType.Integer)&&!double.IsNaN((double)t)&&!double.IsInfinity((double)t);
            bool Integer(JToken t)=>Number(t)&&(double)t==Math.Floor((double)t)&&(double)t>=2&&(double)t<=4096;
            if(!Number(k["version"])||(double)k["version"]!=1||!Integer(k["cols"])||!Integer(k["rows"])||(double)k["cols"]*(double)k["rows"]>4194304)return false;
            foreach(string field in new[]{"cell","units","turn","nod"})if(!Number(k[field])||(double)k[field]<=0)return false;
            if(!(k["keys"] is JArray list)||list.Count>32)return false;
            long cells=(long)(double)k["cols"]*(long)(double)k["rows"],fieldLength=((cells*4+2)/3)*4,maskLength=((cells+2)/3)*4;
            foreach(var key in list)
            {
                if(!(key is JObject)||!new[]{"angleX","angleY","diag"}.Contains((string)key["param"])||!Number(key["value"])||((string)key["param"]!="diag"&&(double)key["value"]==0))return false;
                if(!(key["fields"] is JObject fields)||fields.Properties().Any(p=>p.Value.Type!=JTokenType.String||((string)p.Value).Length!=fieldLength))return false;
                if(key["same"]!=null&&(!(key["same"] is JArray same)||same.Any(v=>v.Type!=JTokenType.String)))return false;
            }
            if(k["mask"]!=null&&k["mask"].Type!=JTokenType.Null&&(!(k["mask"] is JObject masks)||masks.Properties().Any(p=>p.Value.Type!=JTokenType.String||((string)p.Value).Length!=maskLength)))return false;
            return true;
        }
        private static double SampleKey(byte[] bytes,JObject k,double x,double y,int component,bool mask)
        {
            int cols=(int)k["cols"],rows=(int)k["rows"];double gx=Math.Min(cols-1.001,Math.Max(0,x/N(k,"cell"))),gy=Math.Min(rows-1.001,Math.Max(0,y/N(k,"cell")));int c=(int)Math.Floor(gx),r=(int)Math.Floor(gy);double tx=gx-c,ty=gy-r;
            double At(int cc,int rr){int i=rr*cols+cc;if(mask)return bytes[i];i=4*i+2*component;return unchecked((short)(bytes[i]|bytes[i+1]<<8));}
            double result=(At(c,r)*(1-tx)+At(c+1,r)*tx)*(1-ty)+(At(c,r+1)*(1-tx)+At(c+1,r+1)*tx)*ty;
            return mask?result/255:result*N(k,"units");
        }
        // Called after source build samples and resetPhysics, before the first rendered frame.
        public bool PrepareKeyforms(JToken value)
        {
            if(!KeyformsFit(value))return false;var file=(JObject)value;var definitions=(JArray)file["keys"];
            var decoded=new Dictionary<string,byte[]>();
            byte[] Bytes(string text){if(!decoded.TryGetValue(text,out var bytes)){bytes=Convert.FromBase64String(text);decoded.Add(text,bytes);}return bytes;}
            // Decode before changing the pose; malformed base64 must not leave the rig at a key pose.
            int cells=(int)file["cols"]*(int)file["rows"];
            try
            {
                foreach(var key in definitions)foreach(var entry in ((JObject)key["fields"]).Properties())if(Bytes((string)entry.Value).Length!=cells*4)return false;
                if(file["mask"] is JObject masks)foreach(var entry in masks.Properties())if(Bytes((string)entry.Value).Length!=cells)return false;
            }
            catch(FormatException){return false;}
            var savedParams=@params;var savedBody=body;var savedBust=bust;var savedMouth=mouthW;double savedSmile=smileShown;bool savedOff=kfOff;
            var savedSprings=chains.ToDictionary(entry=>entry.Key,entry=>entry.Value.Select(q=>new PuppetSpring{x=q.x,y=q.y,vx=q.vx,vy=q.vy}).ToArray());
            var b=rig["body"];double turn=N(tuning,"turn");keys.Clear();kfPair=null;
            foreach(var layer in layers)layer.kf=new float[definitions.Count][];
            try
            {
                for(int ki=0;ki<definitions.Count;ki++)
                {
                    var key=definitions[ki];string param=(string)key["param"];double v=(double)key["value"];var pose=new Params();if(param!="diag")pose[param]=v;@params=pose;
                    body=new PuppetBody{yaw=pose.bodyX+pose.angleX*N(b,"yaw")*turn,roll=pose.bodyZ+pose.angleZ*N(b,"roll")*turn};
                    foreach(var chain in chains.Values)foreach(var q in chain)q.x=q.y=q.vx=q.vy=0;
                    bust=new PuppetBust{px=savedBust.px,py=savedBust.py};mouthW=new[]{1d,0,0};smileShown=0;kfOff=true;Deform();kfOff=false;
                    var fields=(JObject)key["fields"];var same=key["same"] as JArray;
                    bool Same(string group)=>same!=null&&same.Any(g=>(string)g==group);
                    foreach(var layer in layers)
                    {
                        string id=(string)layer.def["id"],group=(string)layer.def["group"],g=fields.Property(id)!=null?id:group=="head"?"head":group.StartsWith("hair",StringComparison.Ordinal)?"hair":group=="body"&&(fields["torso"]!=null||Same("torso"))?"torso":id;
                        string text=(string)fields[g]??(Same(g)?(string)fields["head"]:null);if(string.IsNullOrEmpty(text))continue;
                        var raw=Bytes(text);string maskText=(string)file["mask"]?[g];var mask=string.IsNullOrEmpty(maskText)?null:Bytes(maskText);var field=new float[layer.rest.Length];
                        for(int i=0;i<field.Length;i+=2){double x=layer.rest[i],y=layer.rest[i+1],weight=mask==null?1:SampleKey(mask,file,x,y,0,true);field[i]=(float)((x+SampleKey(raw,file,x,y,0,false)-layer.output[i])*weight);field[i+1]=(float)((y+SampleKey(raw,file,x,y,1,false)-layer.output[i+1])*weight);}
                        layer.kf[ki]=field;
                    }
                    bool nod=param=="angleY";keys.Add(new PuppetKey{param=param,value=v,scale=turn*(nod?N(tuning,"nod"):1)/(N(file,"turn")*(nod?N(file,"nod"):1))});
                }
                kfPair=(string)file["interp"]=="hermite"?keys.Select(k=>keys.FindIndex(other=>other.param==k.param&&k.param!="diag"&&other.value==-k.value)).ToArray():null;
                return true;
            }
            finally
            {
                @params=savedParams;body=savedBody;bust=savedBust;mouthW=savedMouth;smileShown=savedSmile;kfOff=savedOff;
                foreach(var entry in savedSprings)for(int i=0;i<entry.Value.Length;i++){var from=entry.Value[i];var to=chains[entry.Key][i];to.x=from.x;to.y=from.y;to.vx=from.vx;to.vy=from.vy;}
            }
        }
    }
}

namespace LivLiv.Runtime
{
    public sealed class ViewTransform { public double x,y,s=1; }
    public sealed partial class Puppet
    {
        public double zoom=1,panY;
        public ViewTransform view=new ViewTransform(),move=new ViewTransform();
        public bool cutBottom,focusHead,lidCurve=true;
        public double[] lastView={1,1,0,0,1,1};
        // render()'s model -> clip placement; y is still source-model down, before the vertex shader flips it.
        public double[] CalculateView(double width,double height)
        {
            double mw=(double)model["canvas"][0],mh=(double)model["canvas"][1];var head=model["anchors"]["head"];
            double OffY(double scale,double y)=>(focusHead?-(N(head,"cy")+N(head,"ry")*.15)*(2*scale)/height:-mh*scale/height)+panY-2*y;
            double Bottom(double scale,double oy)=>-(mh*(2*scale)/height+oy);
            double baseline=Math.Min(width/mw,height/mh)*zoom,scale=baseline*view.s*move.s,sx=2*scale/width,sy=2*scale/height,oy=OffY(scale,view.y+move.y);
            if(cutBottom){double allowed=Math.Max(Bottom(baseline*view.s,OffY(baseline*view.s,view.y)),-1);oy+=Math.Max(0,Bottom(scale,oy)-allowed);}
            double ox=(focusHead?-N(head,"cx")*sx:-mw*scale/width)+2*(view.x+move.x);
            lastView=new[]{sx,sy,ox,oy,width,height};return lastView;
        }
        public double[] CanvasToModel(double px,double py,double displayWidth,double displayHeight)
        {
            var v=lastView;double cx=2*px*(v[4]/Math.Max(1,displayWidth))/v[4]-1,cy=2*py*(v[5]/Math.Max(1,displayHeight))/v[5]-1;
            return new[]{(cx-v[2])/v[0],(cy-v[3])/v[1]};
        }
        public int Poke(double x,double y,double strength=1)
        {
            int hit=0;
            foreach(var c in chainRuns)
            {
                var r=c.R;int m=r.Length/2;double reach=1.5*N(c.def,"sigma",40)+20,near=double.PositiveInfinity;
                for(int j=0;j<m;j++)near=Math.Min(near,Hypot(r[2*j]-x,r[2*j+1]-y));if(near>reach)continue;hit++;double side=x<r[0]?1:-1;
                for(int j=1;j<m;j++){double k=(1-near/reach)*c.s[j]*9*strength;c.q[2*j]=(float)(c.q[2*j]-side*k);c.q[2*j+1]=(float)(c.q[2*j+1]+k*.3);}
            }
            return hit;
        }
    }
}

namespace LivLiv.Runtime
{
    // The classic IdleDriver retained by upstream for comparisons. PlayerBase uses follow.ts's LivelyIdle wrapper.
    public sealed class IdleDriver
    {
        public double nextBlink=2,blinkT=-1,blinkRate=1,nextSaccade=1;
        public IdlePoint look=new IdlePoint(),gaze=new IdlePoint();
        private readonly Func<double> random;
        public IdleDriver(Func<double> random=null){this.random=random??new Random().NextDouble;}
        public void Apply(Params p,double t,double dt)
        {
            p.breath=.5+.5*Math.Sin(t*2.1);
            if(blinkT<0&&t>nextBlink)blinkT=0;
            if(blinkT>=0)
            {
                blinkT+=dt;double b=blinkT/.075,open=b<1?1-b:b<2.6?(b-1)/1.6:1;p.eyeLOpen=p.eyeROpen=Math.Max(0,Math.Min(1,open));
                if(b>=2.6){blinkT=-1;nextBlink=t+(1.8+random()*3.8)/Math.Max(.2,blinkRate);}
            }
            if(t>nextSaccade){gaze=new IdlePoint{x=look.x+(random()-.5)*.25,y=look.y*.8+(random()-.5)*.2};nextSaccade=t+.6+random()*1.6;}
            p.eyeBallX+=(Math.Max(-1,Math.Min(1,gaze.x*1.2))-p.eyeBallX)*Math.Min(1,dt*18);p.eyeBallY+=(Math.Max(-1,Math.Min(1,gaze.y))-p.eyeBallY)*Math.Min(1,dt*18);
            double tx=look.x*20+Math.Sin(t*.7)*3.5,ty=look.y*13+Math.Sin(t*.9)*2;
            p.angleX+=(tx-p.angleX)*Math.Min(1,dt*3);p.angleY+=(ty-p.angleY)*Math.Min(1,dt*3);p.angleZ+=(Math.Sin(t*.5)*3-look.x*4-p.angleZ)*Math.Min(1,dt*2);
            p.bodyX+=(look.x*4+Math.Sin(t*.33)*1.5-p.bodyX)*Math.Min(1,dt*2);p.bodyZ+=(Math.Sin(t*.41+1)*1.2-p.bodyZ)*Math.Min(1,dt*2);
            double brow=look.y>.4?.5:0;p.browLY+=(brow-p.browLY)*Math.Min(1,dt*4);p.browRY=p.browLY;
        }
    }
}

namespace LivLiv.Runtime
{
    public sealed partial class Puppet
    {
        // A transactional renderer builds a fresh model while retaining upstream's non-model settings.
        // Mutable values are copied so a failed preparation cannot change the visible puppet.
        public Puppet ForReplacement()
        {
            var next=new Puppet {
                @params=@params,time=time,smileShown=smileShown,physicsScale=physicsScale,
                zoom=zoom,panY=panY,focusHead=focusHead,
                view=new ViewTransform{x=view.x,y=view.y,s=view.s},move=new ViewTransform{x=move.x,y=move.y,s=move.s},
                lastView=(double[])lastView.Clone(),bodyHidden=bodyHidden,headHidden=headHidden,
                procJaw=procJaw,chinFollow=chinFollow,chinNeck=chinNeck,chinNeckX=(double[])chinNeckX.Clone(),
                neckFollow=neckFollow,neckFollowY=neckFollowY,neckFollowX=(double[])neckFollowX.Clone(),
                lidCurve=lidCurve,irisLid=irisLid,irisDrop=irisDrop,irisNasal=irisNasal,irisNasalNarrow=irisNasalNarrow,
                kfOff=kfOff,keyWeights=keyWeights==null?null:(double[])keyWeights.Clone(),
                drive=new PuppetDrive{x=drive.x,y=drive.y,vx=drive.vx,vy=drive.vy,ax=drive.ax,ay=drive.ay}
            };
            next.hidden.UnionWith(hidden);return next;
        }
    }
}
