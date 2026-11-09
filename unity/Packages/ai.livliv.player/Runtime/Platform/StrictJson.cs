// JSON.parse grammar guard for Newtonsoft's intentionally more permissive reader.
// Keeps comments, trailing commas, single quotes, undefined and NaN out of package files.
using Newtonsoft.Json;

namespace LivLiv.Platform
{
    internal sealed class StrictJson
    {
        private readonly string text;
        private int at;
        private StrictJson(string text) { this.text=text; }
        private char Current=>at<text.Length?text[at]:'\0';
        private void Bad() => throw new JsonReaderException("Invalid JSON at character "+at);
        private void Space() { while(Current==' '||Current=='\t'||Current=='\r'||Current=='\n')at++; }
        private void Expect(char c) { if(Current!=c)Bad();at++; }
        private static bool Digit(char c)=>c>='0'&&c<='9';
        private void String()
        {
            Expect('"');
            while(Current!='"')
            {
                if(Current<' ')Bad();
                if(Current=='\\')
                {
                    at++;
                    if(Current=='u')
                    {
                        at++;
                        for(int n=0;n<4;n++,at++)if(!Digit(Current)&&!(Current>='a'&&Current<='f')&&!(Current>='A'&&Current<='F'))Bad();
                        continue;
                    }
                    if(Current!='"'&&Current!='\\'&&Current!='/'&&Current!='b'&&Current!='f'&&Current!='n'&&Current!='r'&&Current!='t')Bad();
                }
                at++;
            }
            at++;
        }
        private void Value(int depth)
        {
            if(depth>128)Bad();Space();
            if(Current=='"'){String();return;}
            if(Current=='{'||Current=='[')
            {
                bool obj=Current=='{';char end=obj?'}':']';at++;Space();
                if(Current==end){at++;return;}
                for(;;)
                {
                    Space();if(obj){String();Space();Expect(':');}
                    Value(depth+1);Space();
                    if(Current==end){at++;return;}
                    Expect(','); // A following end is rejected by String/Value.
                }
            }
            foreach(var literal in new[]{"true","false","null"})
            {
                if(Current!=literal[0])continue;
                foreach(var c in literal)Expect(c);return;
            }
            if(Current=='-')at++;
            if(Current=='0')at++;
            else { if(Current<'1'||Current>'9')Bad();while(Digit(Current))at++; }
            if(Current=='.'){at++;if(!Digit(Current))Bad();while(Digit(Current))at++;}
            if(Current=='e'||Current=='E'){at++;if(Current=='+'||Current=='-')at++;if(!Digit(Current))Bad();while(Digit(Current))at++;}
        }
        public static void Validate(string text)
        {
            var parser=new StrictJson(text);parser.Value(0);parser.Space();
            if(parser.at!=text.Length)parser.Bad();
        }
    }
}
