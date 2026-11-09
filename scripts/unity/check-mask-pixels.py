"""Compare full-target baseline PNGs with cropped-mask results on the same backend."""
from pathlib import Path
from PIL import Image
import hashlib,json
root=Path(__file__).resolve().parents[2];results=root/'unity/TestResults'
report={}
for name,folder,pattern in [('metal','mask-baseline-frames','animated-*.png'),('webgl','mask-webgl-baseline','webgl-snapshot-*-raw.png')]:
 rows=[]
 for before in sorted((results/folder).glob(pattern)):
  after=results/before.name
  with Image.open(before) as a,Image.open(after) as b:
   assert a.size==b.size
   aa=a.convert('RGBA').tobytes();bb=b.convert('RGBA').tobytes();diff=[abs(x-y) for x,y in zip(aa,bb)]
   row={'file':before.name,'maxChannelLsb':max(diff),'meanChannelLsb':sum(diff)/len(diff),'changedPixels':sum(any(diff[i:i+4]) for i in range(0,len(diff),4)),'pixels':len(diff)//4,'beforeSha256':hashlib.sha256(before.read_bytes()).hexdigest(),'afterSha256':hashlib.sha256(after.read_bytes()).hexdigest()}
   rows.append(row)
 report[name]=rows
assert len(report['metal'])==12 and len(report['webgl'])==4
# Projection reassociation may round interpolated coverage differently at a boundary.
(results/'mask-pixel-comparison.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
assert all(row['maxChannelLsb']<=2 for rows in report.values() for row in rows), 'Cropped mask changed more than 2 LSB; inspect evidence'
