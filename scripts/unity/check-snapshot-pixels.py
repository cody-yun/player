"""Compare saved straight-alpha snapshots to their same-frame premultiplied GPU captures."""
from pathlib import Path
import hashlib
import json
from PIL import Image

root = Path(__file__).resolve().parents[2]
results = root / 'unity/TestResults'
report = {}
for pose in ['neutral', 'turn', 'blink', 'talk']:
    snapshot = results / f'webgl-snapshot-{pose}.png'
    raw = results / f'webgl-snapshot-{pose}-raw.png'
    with Image.open(snapshot) as a, Image.open(raw) as b:
        assert a.size == b.size, pose
        aa, bb = a.convert('RGBA').tobytes(), b.convert('RGBA').tobytes()
        maximum = max(abs(aa[i + k] * aa[i + 3] / 255 - bb[i + k])
                      for i in range(0, len(aa), 4) for k in range(3))
        alpha = max(abs(aa[i] - bb[i]) for i in range(3, len(aa), 4))
        assert maximum <= 1 and alpha == 0, (pose, maximum, alpha)
        report[pose] = {'maxPremultipliedRoundtripLsb': maximum, 'maxAlphaDifference': alpha,
                        'pixels': a.width * a.height,
                        'pngSha256': hashlib.sha256(snapshot.read_bytes()).hexdigest(),
                        'rawSha256': hashlib.sha256(raw.read_bytes()).hexdigest()}
(results / 'snapshot-pixel-check.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
