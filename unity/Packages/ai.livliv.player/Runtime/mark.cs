// Port of src/mark.ts selection/cache contract. GPU canvas lifecycle is in UnityMark.
// Copyright 2026 LivLiv. Apache-2.0.
using System;
using System.Collections.Generic;
using LivLiv.Runtime;

namespace LivLiv
{
    public sealed class Mark
    {
        private bool logo,center=true;
        private string text;
        public bool Visible=>logo||!string.IsNullOrEmpty(text);
        public void Set(string kind,string text,bool center=true)
        { logo=kind=="livliv";this.text=kind=="text"?text:null;this.center=center; }
        public string CacheKey(int w,int h,double dpr,double ms)
            => FormattableString.Invariant($"{w}x{h}:{dpr}:{(logo?Math.Floor(Brand.EyeOpen(ms)*10+.5).ToString(System.Globalization.CultureInfo.InvariantCulture):"")}:{(logo?"true":"false")}:{(center?"true":"false")}:{(logo?Brand.BrandMark:text)}");
        public List<BrandDraw> Draw(int w,int h,double ms,double dpr=1)
        {
            var commands=new List<BrandDraw>();
            if(logo&&center)Brand.DrawWatermark(commands,w,h,ms,dpr);
            else if(logo)Brand.DrawCornerLogo(commands,w,h,ms,dpr);
            else if(!string.IsNullOrEmpty(text))Brand.DrawMarkText(commands,w,h,text,dpr);
            return commands;
        }
    }
}
