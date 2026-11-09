"""Synthetic stereo decoder fixtures: left 330 Hz, right 770 Hz, no recorded voice.

Requires ffmpeg with libmp3lame. --check regenerates in a temporary directory and compares.
"""
from pathlib import Path
import hashlib
import json
import math
import shutil
import struct
import subprocess
import sys
import tempfile

root = Path(__file__).resolve().parents[2]
dest = root / 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures'
ffmpeg = shutil.which('ffmpeg')
assert ffmpeg, 'ffmpeg is required'


def wave(encoding, bits):
    rate = 24000
    data = bytearray()
    for i in range(rate // 3):
        for hz in (330, 770):
            value = .2 * math.sin(2 * math.pi * hz * i / rate)
            data.extend(struct.pack('<h', round(value * 32767)) if bits == 16 else struct.pack('<f', value))
    fmt = struct.pack('<HHIIHH', encoding, 2, rate, rate * 2 * bits // 8, 2 * bits // 8, bits)
    body = b'WAVEfmt ' + struct.pack('<I', 16) + fmt + b'data' + struct.pack('<I', len(data)) + data
    return b'RIFF' + struct.pack('<I', len(body)) + body


with tempfile.TemporaryDirectory(prefix='livliv-speech-fixtures-') as directory:
    tmp = Path(directory)
    (tmp / 'speech-stereo.wav.bytes').write_bytes(wave(1, 16))
    (tmp / 'speech-float.wav.bytes').write_bytes(wave(3, 32))
    subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-i', str(tmp / 'speech-stereo.wav.bytes'),
                    '-codec:a', 'libmp3lame', '-b:a', '64k', '-write_xing', '0', '-map_metadata', '-1',
                    '-f', 'mp3', '-y', str(tmp / 'speech-stereo.mp3.bytes')], check=True)
    hashes = {}
    for file in tmp.glob('*.bytes'):
        data = file.read_bytes()
        if '--check' in sys.argv:
            assert (dest / file.name).read_bytes() == data, file.name + ' differs'
        else:
            dest.mkdir(parents=True, exist_ok=True)
            (dest / file.name).write_bytes(data)
        hashes[file.name] = hashlib.sha256(data).hexdigest()
    print(json.dumps({'generator': 'synthetic sine, 24000 Hz stereo 1/3 second',
                      'ffmpeg': subprocess.check_output([ffmpeg, '-version'], text=True).splitlines()[0],
                      'sha256': hashes}, indent=2))
