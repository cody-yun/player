"""Verify owner text changes only the source corner and survives PNG capture."""
from pathlib import Path
from PIL import Image
import argparse,hashlib,json
parser=argparse.ArgumentParser();parser.add_argument("--directory",type=Path);parser.add_argument("--prefix",default="webgl-text-");args=parser.parse_args()
r=args.directory or Path(__file__).resolve().parents[2]/'unity/TestResults';report={}
for name in ['latin','korean','literal','default','retina']:
 density=2 if name=='retina' else 1
 path=r/f'{args.prefix}{name}.png';base=r/(args.prefix+('retina-empty.png' if density==2 else 'empty.png'))
 with Image.open(path) as a,Image.open(base) as b:
  assert a.size==b.size==(512*density,768*density)
  aa=a.convert('RGBA').tobytes();bb=b.convert('RGBA').tobytes();points=[]
  for i in range(0,len(aa),4):
   if aa[i:i+4]!=bb[i:i+4]:points.append((i//4%a.width,i//4//a.width))
  assert len(points)>50*density*density,(name,'text disappeared')
  box=[min(p[0] for p in points),min(p[1] for p in points),max(p[0] for p in points)+1,max(p[1] for p in points)+1]
  assert box[0]>250*density and box[1]>700*density and box[2]<512*density and box[3]<768*density,(name,box)
  report[name]={'changedPixels':len(points),'bounds':box,'width':a.width,'height':a.height,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
(r/'text-mark-pixels.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
