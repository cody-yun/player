"""Strict fixed-clock RGBA and per-phase timing comparison, without watermark exclusions."""
from pathlib import Path
from PIL import Image, ImageChops
import argparse
import json
import statistics

parser = argparse.ArgumentParser()
parser.add_argument('before', type=Path)
parser.add_argument('after', type=Path)
args = parser.parse_args()
a = json.loads((args.before / 'evidence.json').read_text())
b = json.loads((args.after / 'evidence.json').read_text())
assert a['result'] == b['result'] == 'Passed'
for key in ['models', 'platform', 'graphics', 'device', 'width', 'height', 'frames', 'warmup']:
    assert a[key] == b[key], key
changed_sources = [key for key in a['sourceHashes'] if a['sourceHashes'][key] != b['sourceHashes'].get(key)]
assert changed_sources == ['unity/Packages/ai.livliv.player/Runtime/runtime/puppet.cs'], changed_sources
report = {'before': str(args.before.resolve()), 'after': str(args.after.resolve()), 'changedSources': changed_sources,
          'scope': '512x768 premultiplied RGBA; 21 models, same generated pose, watermark clock 0; CPU phases exclude input/drivers and GPU completion.',
          'images': {}, 'means': {}}
for name in a['models']:
    with Image.open(args.before / (name + '.png')) as x, Image.open(args.after / (name + '.png')) as y:
        assert x.size == y.size == (512, 768)
        x, y = x.convert('RGBA'), y.convert('RGBA')
        diff = ImageChops.difference(x, y)
        report['images'][name] = {'identical': x.tobytes() == y.tobytes(),
                                 'maximumChannelDifference': max(hi for lo, hi in diff.getextrema())}
for key in ['physicsMeanMs', 'deformMeanMs', 'renderMeanMs']:
    old, new = statistics.mean(row[key] for row in a['rows']), statistics.mean(row[key] for row in b['rows'])
    report['means'][key] = {'before': old, 'after': new, 'speedup': old / new}
report['result'] = 'Passed' if all(row['identical'] for row in report['images'].values()) else 'Failed'
(args.after / 'previous-comparison.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
assert report['result'] == 'Passed', 'Fixed-clock output changed'
