"""Synthetic JPEG codec fixtures; no upstream character assets. Requires Pillow."""
from pathlib import Path
from PIL import Image

folder = Path(__file__).resolve().parents[2] / 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures'
image = Image.new('RGB', (32, 48))
image.paste((255, 0, 0), (0, 0, 32, 24))
image.paste((0, 0, 255), (0, 24, 32, 48))
for progressive in [False, True]:
    name = 'progressive' if progressive else 'baseline'
    image.save(folder / ('poster-' + name + '.jpg'), quality=95, subsampling=0, progressive=progressive)
