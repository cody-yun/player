#!/usr/bin/env python3
"""Loopback-only model transport fixtures + WebGL host. No hardware or external service."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from urllib.parse import urlsplit
import io, zipfile, time, threading, json
ROOT=Path(__file__).resolve().parents[2]
B=io.BytesIO()
with zipfile.ZipFile(B,'w',zipfile.ZIP_DEFLATED) as z:
    for p in sorted((ROOT/'characters/char06').rglob('*')):
        if p.is_file(): z.write(p,p.relative_to(ROOT/'characters/char06'))
PACKAGE=B.getvalue()
COUNTS={}; LOCK=threading.Lock()
class Handler(SimpleHTTPRequestHandler):
    def __init__(self,*a,**kw): super().__init__(*a,directory=str(ROOT/'unity/TestResults/WebGL'),**kw)
    def log_message(self,*a): pass
    def do_GET(self):
        path=urlsplit(self.path).path
        with LOCK: COUNTS[path]=COUNTS.get(path,0)+1; count=COUNTS[path]
        if path=='/counts':
            with LOCK: body=json.dumps(COUNTS).encode()
            self.reply(body);return
        if path.startswith('/models/'):
            file=(ROOT/'characters'/path[len('/models/'):]).resolve()
            if not file.is_relative_to(ROOT/'characters') or not file.is_file():self.send_error(404);return
            self.reply(file.read_bytes());return
        if path in ['/package.zip','/retry.zip','/slow.zip','/unknown.zip']:
            if path=='/retry.zip' and count%2==1:self.send_error(503);return
            self.reply(PACKAGE,slow=path=='/slow.zip',length=path!='/unknown.zip');return
        if path=='/bad.zip':self.reply(b'not a zip');return
        if path=='/tiny-cap':self.reply(b'x'*10000);return
        super().do_GET()
    def reply(self,body,slow=False,length=True):
        self.send_response(200);self.send_header('Content-Type','application/octet-stream')
        if length:self.send_header('Content-Length',str(len(body)))
        self.end_headers()
        try:
            for at in range(0,len(body),16384):
                self.wfile.write(body[at:at+16384]);self.wfile.flush()
                if slow:time.sleep(.05)
        except (BrokenPipeError,ConnectionResetError):pass
if __name__=='__main__':
    print('load verification http://127.0.0.1:8771',flush=True)
    ThreadingHTTPServer(('127.0.0.1',8771),Handler).serve_forever()
