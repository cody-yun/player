"""Compare browser straight-alpha PNG with Unity premultiplied render-target PNG.
Diagnostic only: prints errors over the union of nontransparent pixels, not a pass/fail claim.
Requires Pillow. Outputs stay in ignored Unity TestResults.
"""
import json
from pathlib import Path
from PIL import Image

root = Path(__file__).resolve().parents[2] / 'unity/TestResults'
rows = []
for reference in sorted(root.glob('browser-*.png')):
    target = root / reference.name.removeprefix('browser-')
    if not target.exists():
        continue
    a_image = Image.open(reference).convert('RGBA')
    b_image = Image.open(target).convert('RGBA')
    if a_image.size != b_image.size:
        raise ValueError(f'{target.name}: dimensions differ')
    a, b = list(a_image.get_flattened_data()), list(b_image.get_flattened_data())
    count = total = alpha_error = maximum = over16 = alpha_a = alpha_b = 0
    for aa, bb in zip(a, b):
        alpha_a += aa[3]
        alpha_b += bb[3]
        if not aa[3] and not bb[3]:
            continue
        count += 1
        d = [abs(aa[c] * aa[3] / 255 - bb[c]) for c in range(3)] + [abs(aa[3] - bb[3])]
        total += sum(d)
        alpha_error += d[3]
        maximum = max(maximum, max(d))
        over16 += max(d) > 16
    rows.append(dict(name=target.name, size=a_image.size, active_pixels=count,
                     mae_active=total / (count * 4) if count else 0,
                     alpha_mae_active=alpha_error / count if count else 0,
                     maximum=maximum, over16_fraction=over16 / count if count else 0,
                     alpha_sum_ratio=alpha_b / alpha_a if alpha_a else None))
result = json.dumps(rows, indent=2) + '\n'
(root / 'render-comparison.json').write_text(result)
print(result)
