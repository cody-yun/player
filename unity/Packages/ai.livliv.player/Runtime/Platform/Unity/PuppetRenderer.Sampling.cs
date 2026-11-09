// Image-derived build() inputs. Source numeric helpers remain in runtime/puppet.cs.
using System;
using System.Collections.Generic;
using System.Linq;
using LivLiv.Platform;
using LivLiv.Runtime;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace LivLiv.Unity
{
    public sealed partial class PuppetRenderer
    {
        private static void PrepareSamples(Puppet core,List<Layer> layers)
        {
            // Cache readbacks only for this build; GPU textures retain no extra full-size CPU source copy here.
            var alpha=new Dictionary<Texture2D,AlphaImage>();
            AlphaImage Read(Texture2D texture){if(!alpha.TryGetValue(texture,out var image)){image=LayerSampling.Alpha(texture);alpha.Add(texture,image);}return image;}
            var bangs=layers.FirstOrDefault(l=>(string)l.def["group"]=="hair_front");
            if(bangs!=null)
            foreach(var layer in layers)
            {
                var d=layer.def;bool brow=(string)d["id"]=="brows"||(string)d["role"]=="expr_brows";
                if(!brow&&layer.brows==null)continue;
                int w=(int)d["w"],h=(int)d["h"];var u=bangs.def;
                var under=AlphaRaster.Draw(new[]{Read(bangs.main)},w,h,(double)u["x"]-(double)d["x"],(double)u["y"]-(double)d["y"],(double)u["w"],(double)u["h"]);
                var grid=new LayerLattice(core.model,d);
                float[] Cover(Texture2D texture)=>Puppet.CoverAt(AlphaRaster.Draw(new[]{Read(texture)},w,h,0,0,w,h),under,d,layer.geometry.rest,grid.cell);
                layer.geometry=core.BuildLayer(d,layer.gridAlpha,brow?Cover(layer.main):null,layer.brows!=null?Cover(layer.brows):null,bangs.def);
                if(layer.geometry.folB!=null)layer.geometry.outB=new float[layer.geometry.rest.Length];
            }
            core.layers.Clear();core.layers.AddRange(layers.Select(l=>l.geometry));
            var eyes=core.model["anchors"]["eyes"] as JArray;
            if(eyes!=null&&eyes.Count>=2)
            {
                double mid=eyes.Sum(e=>((double)e[0]+(double)e[2])/2)/eyes.Count;
                foreach(var layer in layers)
                {
                    var d=layer.def;string role=(string)d["role"]??"",side=role.StartsWith("iris_",StringComparison.Ordinal)?role.Substring(5):"";
                    var parent=layers.FirstOrDefault(l=>(string)l.def["id"]==(string)d["clip"]);
                    if(side.Length==0||!(d["range"] is JArray)||parent?.mask==null||core.nasal.ContainsKey(side))continue;
                    var iris=Read(layer.main);var mask=Read(parent.mask);
                    var n=Puppet.NasalRoom(iris.alpha,iris.width,iris.height,d,mask.alpha,mask.width,mask.height,parent.def,mid>(double)d["x"]+(double)d["w"]/2?1:-1);
                    if(n!=null)core.nasal[side]=n;
                }
            }
            if(core.lidCurve&&Puppet.LidCurveFits(core.model))foreach(string side in new[]{"L","R"})
            {
                var lid=layers.FirstOrDefault(l=>(string)l.def["role"]=="lid_"+side);if(lid==null)continue;
                var line=Puppet.ClosedLashLine(lid.lashRgba,lid.main.width,lid.main.height,lid.def);if(line==null)continue;
                foreach(var layer in layers)
                {
                    string role=(string)layer.def["role"];if(role!="eye_"+side&&role!="iris_"+side&&role!="shine_"+side)continue;
                    var geometry=layer.geometry;geometry.pivY=new float[geometry.rest.Length/2];for(int i=0;i<geometry.pivY.Length;i++)geometry.pivY[i]=(float)line(geometry.rest[2*i]);
                    geometry.pivC=line((double)layer.def["x"]+(double)layer.def["w"]/2);
                }
            }
            foreach(var layer in layers){layer.lashRgba=null;layer.gridAlpha=null;}
        }
    }
}
