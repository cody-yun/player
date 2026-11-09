"""Compare identical lifecycle runs before/after a numeric optimization; requires Pillow."""
from pathlib import Path
from PIL import Image, ImageChops
import argparse
import json
import statistics

parser = argparse.ArgumentParser()
parser.add_argument('before', type=Path)
parser.add_argument('after', type=Path)
args = parser.parse_args()
before = json.loads((args.before / 'evidence.json').read_text())
after = json.loads((args.after / 'evidence.json').read_text())
assert before['result'] == after['result'] == 'Passed'
assert before['models'] == after['models'], 'Input models changed'
assert before['platform'] == after['platform'] and before['graphics'] == after['graphics']
report = {'before': str(args.before.resolve()), 'after': str(args.after.resolve()), 'images': {}, 'passes': []}
for name in before['models']:
    with Image.open(args.before / (name + '.png')) as a, Image.open(args.after / (name + '.png')) as b:
        assert a.size == b.size
        diff = ImageChops.difference(a.convert('RGBA'), b.convert('RGBA'))
        maximum = max(hi for lo, hi in diff.getextrema())
        mask = diff.getchannel('R')
        for channel in ['G', 'B', 'A']:
            mask = ImageChops.lighter(mask, diff.getchannel(channel))
        changed = mask.width * mask.height - mask.histogram()[0]
        report['images'][name] = {'changedPixels': changed, 'maximumChannelDifference': maximum,
                                  'differenceBounds': mask.getbbox()}
for index in range(3):
    a = [r for r in before['rows'] if r['pass'] == index]
    b = [r for r in after['rows'] if r['pass'] == index]
    assert [(r['replacement'], r['width'], r['height'], r['frames']) for r in a] == [(r['replacement'], r['width'], r['height'], r['frames']) for r in b]
    old, new = statistics.mean(r['tickMeanMs'] for r in a), statistics.mean(r['tickMeanMs'] for r in b)
    report['passes'].append({'pass': index, 'beforeTickMeanMs': old, 'afterTickMeanMs': new, 'speedup': old / new})
report['result'] = 'Passed' if all(row['changedPixels'] == 0 for row in report['images'].values()) else 'Failed'
(args.after / 'previous-comparison.json').write_text(json.dumps(report, indent=2) + '\n')
print(json.dumps(report, indent=2))
assert report['result'] == 'Passed', 'Optimization changed rendered pixels'
