using System;
namespace LivLiv.Platform
{
    // Read dimensions before Unity allocates decoded pixels. Segment lengths include their two length bytes.
    public static class JpegHeader
    {
        public static bool Matches(byte[] bytes)=>bytes!=null&&bytes.Length>=2&&bytes[0]==255&&bytes[1]==216;
        public static void Read(byte[] bytes,out int width,out int height)
        {
            width=height=0;
            if(!Matches(bytes))throw new LivLivError("invalid","Invalid JPEG signature");
            int offset=2;
            while(offset<bytes.Length)
            {
                if(bytes[offset++]!=255)throw new LivLivError("invalid","Invalid JPEG marker");
                while(offset<bytes.Length&&bytes[offset]==255)offset++;
                if(offset>=bytes.Length)break;
                int marker=bytes[offset++];
                if(marker==0||marker==217||marker==218)break;
                if(marker==1||marker==216||marker>=208&&marker<=215)continue;
                if(offset+2>bytes.Length)break;
                int length=bytes[offset]*256+bytes[offset+1];
                if(length<2||length>bytes.Length-offset)break;
                bool frame=marker>=192&&marker<=207&&marker!=196&&marker!=200&&marker!=204;
                if(frame)
                {
                    if(length<8||bytes[offset+7]==0||length!=8+3*bytes[offset+7])break;
                    height=bytes[offset+3]*256+bytes[offset+4];width=bytes[offset+5]*256+bytes[offset+6];
                    if(width==0||height==0)break;
                    return;
                }
                offset+=length;
            }
            throw new LivLivError("invalid","Invalid or truncated JPEG frame header");
        }
    }
}
