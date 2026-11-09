"""Record native/backend pixel differences and verify cropped/full WebGL equivalence."""
from pathlib import Path
from PIL import Image,ImageChops,ImageStat
import hashlib,json
root=Path(__file__).resolve().parents[2];results=root/'unity/TestResults'
def compare(a,b):
 with Image.open(a) as x,Image.open(b) as y:
  assert x.size==y.size
  diff=ImageChops.difference(x.convert('RGBA'),y.convert('RGBA'))
  return {'width':x.width,'height':x.height,'maxChannelLsb':max(t[1] for t in diff.getextrema()),'meanChannelLsb':sum(ImageStat.Stat(diff).mean)/4,
   'a':str(a.relative_to(root)),'b':str(b.relative_to(root)),'shaA':hashlib.sha256(a.read_bytes()).hexdigest(),'shaB':hashlib.sha256(b.read_bytes()).hexdigest()}
report={'webglFullVsCropped':{},'nativeVsWebgl':{},'nativeReuse':{}}
for pose in ['glance','halfblink','combined','offscreen']:
 row=compare(results/f'webgl-mask-full-{pose}.png',results/f'webgl-mask-{pose}.png');report['webglFullVsCropped'][pose]=row
 assert row['maxChannelLsb']==0,(pose,row)
for platform,group in [('macos','macos-presentation'),('android','bluestacks/presentation'),('linux','linux-presentation')]:
 evidence=sorted((results/group).glob('*/evidence.json'))[-1];data=json.loads(evidence.read_text());assert data['result']=='Passed' and len(data['masks'])==8
 report['nativeVsWebgl'][platform]={pose:compare(evidence.parent/f'mask-{pose}.png',results/f'webgl-mask-{pose}.png') for pose in ['glance','halfblink','combined']}
 report['nativeReuse'][platform]={pose:compare(evidence.parent/'mask-combined.png',evidence.parent/f'mask-{pose}.png') for pose in ['reused','smaller','recovered']}
 for pose,row in report['nativeReuse'][platform].items():assert row['maxChannelLsb']<=2,(platform,pose,row)
 # Cross-backend differences are measured, not assumed to be byte-identical.
(results/'native-mask-pixels.json').write_text(json.dumps(report,indent=2)+'\n')
for section,rows in report.items():
 print(section,{k:({'max':v['maxChannelLsb'],'mean':v['meanChannelLsb']} if 'maxChannelLsb' in v else {n:{'max':r['maxChannelLsb'],'mean':r['meanChannelLsb']} for n,r in v.items()}) for k,v in rows.items()})
