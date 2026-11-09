// Port of src/zip.ts. Copyright 2026 LivLiv. Apache-2.0.
// Browser Blob/DecompressionStream replaced by byte[]/bounded DeflateStream.
// Like upstream, this does not authenticate contents or verify CRCs. No files are extracted to disk.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using LivLiv.Runtime;

namespace LivLiv
{
    public static class LivLivZip
    {
        public const int Entries = 2048, Bytes = 256 * (1 << 20);
        private static LivLivError Fail(string why, string code = "invalid") => new LivLivError(code,"LivLiv: "+why);
        public static bool IsZip(byte[] b) => b != null && b.Length >= 4 && b[0]==0x50 && b[1]==0x4b && ((b[2]==3 && b[3]==4)||(b[2]==5 && b[3]==6));
        private static void Bound(byte[] b, long start, long count)
        { if (start < 0 || count < 0 || start > b.Length || count > b.Length-start) throw Fail("the package is damaged (entry bounds)"); }
        private static ushort U16(byte[] b, long o) { Bound(b,o,2); return (ushort)(b[o]|b[o+1]<<8); }
        private static uint U32(byte[] b, long o) { Bound(b,o,4); return (uint)(b[o]|b[o+1]<<8|b[o+2]<<16|b[o+3]<<24); }
        public static Dictionary<string,byte[]> Unzip(byte[] zip, CancellationToken cancellation = default)
        {
            if (zip == null) throw new ArgumentNullException(nameof(zip));
            int e=-1;
            for (int i=zip.Length-22; i>=Math.Max(0,zip.Length-22-0xffff); i--) if (U32(zip,i)==0x06054b50) { e=i; break; }
            if (e<0) throw Fail("this is not a ZIP (no end record)","not-a-model");
            int count=U16(zip,e+10); long cdSize=U32(zip,e+12), cdOff=U32(zip,e+16);
            if (count==0xffff || cdSize==0xffffffff || cdOff==0xffffffff) throw Fail("ZIP64 packages are not supported","too-large");
            if (count>Entries) throw Fail($"the package has {count} entries, more than {Entries}","too-large");
            Bound(zip,cdOff,cdSize);
            var output=new Dictionary<string,byte[]>(StringComparer.Ordinal); long total=0, o=cdOff;
            for (int n=0;n<count;n++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (o+46>cdOff+cdSize || U32(zip,o)!=0x02014b50) throw Fail("the package is damaged (central directory)");
                int flags=U16(zip,o+8), method=U16(zip,o+10), nlen=U16(zip,o+28), elen=U16(zip,o+30), clen=U16(zip,o+32);
                long csize=U32(zip,o+20), usize=U32(zip,o+24), local=U32(zip,o+42);
                Bound(zip,o+46,nlen);
                if (o+46+nlen+elen+clen>cdOff+cdSize) throw Fail("the package is damaged (central directory entry)");
                var raw=Encoding.UTF8.GetString(zip,(int)o+46,nlen); o+=46+nlen+elen+clen;
                var name=raw.Replace('\\','/'); var basename=name.Substring(name.LastIndexOf('/')+1);
                if (name.EndsWith("/",StringComparison.Ordinal) || name.StartsWith("__MACOSX/",StringComparison.Ordinal) || basename.StartsWith("._",StringComparison.Ordinal) || basename==".DS_Store" || basename=="Thumbs.db") continue;
                if (name.StartsWith("/",StringComparison.Ordinal) || Regex.IsMatch(name,@"^[A-Za-z]:") || name.Split('/').Any(s=>s==".." || s==".")) throw Fail("the package holds a path outside itself: "+raw);
                if ((flags&1)!=0) throw Fail(name+" is encrypted");
                if (output.ContainsKey(name)) throw Fail(name+" is in the package twice");
                int cap=basename=="model.json" ? ModelFormat.Limits.ModelJson : basename=="keyforms.json" ? ModelFormat.Limits.Keyforms : Bytes;
                total+=usize;
                if (usize>cap || total>Bytes) throw Fail($"{name} is too large ({usize} bytes)","too-large");
                Bound(zip,local,30);
                if (U32(zip,local)!=0x04034b50) throw Fail("the package is damaged ("+name+")");
                long start=local+30+U16(zip,local+26)+U16(zip,local+28); Bound(zip,start,csize);
                if (method==0)
                {
                    if (csize!=usize) throw Fail("the package is damaged ("+name+")");
                    var bytes=new byte[(int)csize]; Buffer.BlockCopy(zip,(int)start,bytes,0,bytes.Length); output.Add(name,bytes);
                }
                else if (method==8) output.Add(name,Inflate(zip,(int)start,(int)csize,(int)usize,name,cancellation));
                else throw Fail($"{name} uses ZIP method {method}; a package stores its files or deflates them");
            }
            return output;
        }
        private static byte[] Inflate(byte[] zip,int start,int count,int size,string name,CancellationToken cancellation)
        {
            try
            {
                using (var input=new MemoryStream(zip,start,count,false))
                using (var stream=new DeflateStream(input,CompressionMode.Decompress))
                using (var output=new MemoryStream())
                {
                    // Do not allocate the directory's full declared size before any bytes arrive.
                    var buf=new byte[8192]; int n;
                    while ((n=stream.Read(buf,0,buf.Length))>0)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (output.Length+n>size) throw Fail($"{name} inflates past its {size} bytes");
                        output.Write(buf,0,n);
                    }
                    if (output.Length!=size) throw Fail($"the package is damaged ({name}: {output.Length} bytes, {size} expected)");
                    return output.ToArray();
                }
            }
            catch (InvalidDataException e) { throw new LivLivError("invalid","LivLiv: the package is damaged ("+name+")",cause:e); }
        }
    }
}
