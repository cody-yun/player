"""Loopback WebGL host and gated WAV response for genuine first-piece playback checks."""
from http.server import ThreadingHTTPServer, SimpleHTTPRequestHandler
from pathlib import Path
from urllib.parse import urlsplit, parse_qs
import json
import threading

ROOT = Path(__file__).resolve().parents[2]
FIXTURES = ROOT / 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures'
STATES = {}
LOCK = threading.Lock()


class Handler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kw):
        super().__init__(*args, directory=str(ROOT / 'unity/TestResults/WebGL'), **kw)

    def log_message(self, *args):
        pass

    def do_GET(self):
        url = urlsplit(self.path)
        key = parse_qs(url.query).get('id', ['default'])[0]
        if url.path == '/speech/state':
            with LOCK:
                body = {k: {f: v for f, v in row.items() if f != 'gate'} for k, row in STATES.items()}
            self.reply(json.dumps(body).encode(), 'application/json')
            return
        if url.path == '/speech/release':
            with LOCK:
                row = STATES.get(key)
                if row:
                    row['gate'].set()
            self.reply(b'ok', 'text/plain')
            return
        files = {'/speech/line.wav': ('speech-stereo.wav.bytes', 'audio/wav'),
                 '/speech/line.mp3': ('speech-stereo.mp3.bytes', 'audio/mpeg'),
                 '/speech/float.wav': ('speech-float.wav.bytes', 'audio/wav'),
                 '/speech/slow.wav': ('speech-stereo.wav.bytes', 'audio/wav')}
        if url.path in files:
            file, mime = files[url.path]
            body = (FIXTURES / file).read_bytes()
            if url.path != '/speech/slow.wav':
                self.reply(body, mime)
                return
            row = {'gate': threading.Event(), 'prefix': False, 'completed': False, 'disconnected': False}
            with LOCK:
                STATES[key] = row
            self.send_response(200)
            self.send_header('Content-Type', mime)
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            try:
                self.wfile.write(body[:9644])  # header + 100ms of PCM
                self.wfile.flush()
                with LOCK:
                    row['prefix'] = True
                if not row['gate'].wait(30):
                    return
                self.wfile.write(body[9644:])
                self.wfile.flush()
                with LOCK:
                    row['completed'] = True
            except (BrokenPipeError, ConnectionResetError):
                with LOCK:
                    row['disconnected'] = True
            return
        if url.path == '/speech/invalid':
            self.reply(b'not audio', 'audio/mpeg')
            return
        super().do_GET()

    def reply(self, data, mime):
        self.send_response(200)
        self.send_header('Content-Type', mime)
        self.send_header('Content-Length', str(len(data)))
        self.end_headers()
        try:
            self.wfile.write(data)
        except (BrokenPipeError, ConnectionResetError):
            pass


if __name__ == '__main__':
    print('speech verification http://127.0.0.1:8772', flush=True)
    ThreadingHTTPServer(('127.0.0.1', 8772), Handler).serve_forever()
