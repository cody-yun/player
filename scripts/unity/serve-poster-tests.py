"""Loopback model/poster gates plus latest WebGL build; no external requests or hardware."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from urllib.parse import urlsplit,parse_qs
import io,json,zipfile,threading
ROOT=Path(__file__).resolve().parents[2]
buffer=io.BytesIO()
with zipfile.ZipFile(buffer,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted((ROOT/'characters/char06').rglob('*')):
        if p.is_file():z.write(p,p.relative_to(ROOT/'characters/char06'))
PACKAGE=buffer.getvalue();GATES={};COUNTS={};LOCK=threading.Lock()
class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*a,**kw):super().__init__(*a,directory=str(ROOT/'unity/TestResults/WebGL'),**kw)
    def log_message(self,*a):pass
    def do_GET(self):
        u=urlsplit(self.path);key=parse_qs(u.query).get('id',[''])[0]
        with LOCK:COUNTS[self.path]=COUNTS.get(self.path,0)+1
        if u.path=='/release':
            with LOCK:gate=GATES.get(key)
            if gate:gate.set()
            self.reply(b'ok');return
        if u.path=='/counts':
            with LOCK:b=json.dumps(COUNTS).encode()
            self.reply(b);return
        if u.path=='/model.zip':self.reply(PACKAGE,key);return
        if u.path=='/poster/avatar.webp':self.reply((ROOT/'characters/char06/avatar.webp').read_bytes(),key);return
        if u.path=='/poster/invalid':self.reply(b'not an image');return
        if u.path=='/missing.zip':self.send_error(404);return
        super().do_GET()
    def reply(self,body,key=''):
        gate=None
        if key:
            with LOCK:gate=GATES.setdefault(key,threading.Event())
        self.send_response(200);self.send_header('Content-Type','application/octet-stream');self.send_header('Content-Length',str(len(body)));self.end_headers()
        try:
            if gate:
                self.wfile.write(body[:16]);self.wfile.flush();gate.wait(30);body=body[16:]
            self.wfile.write(body);self.wfile.flush()
        except (BrokenPipeError,ConnectionResetError):pass
if __name__=='__main__':
    print('poster verification http://127.0.0.1:8773',flush=True)
    ThreadingHTTPServer(('127.0.0.1',8773),Handler).serve_forever()
