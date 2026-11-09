"""Summarize measured per-cycle timings and verify saved model frames; requires Pillow."""
from pathlib import Path
from PIL import Image
import argparse,hashlib,json,statistics
parser=argparse.ArgumentParser();parser.add_argument('directory',type=Path);args=parser.parse_args()
directory=args.directory;data=json.loads((directory/'evidence.json').read_text());assert data['result']=='Passed'
rows=data['rows'];assert len(rows)==63 and data['modelCount']==21
summary={'result':'Passed','platform':data['platform'],'graphics':data['graphics'],'device':data['device'],'cpu':data['cpu'],
 'elapsedSeconds':data['elapsedSeconds'],'cycles':len(rows),'animatedFrames':sum(r['frames'] for r in rows),
 'totalLoads':len(rows)*2,'retainedOwnedRenderObjects':max(r['remainingObjects'] for r in rows),'passes':[],'frames':{},
 'scope':'CPU Tick includes render command submission, not GPU completion. Frame intervals include scheduling. Per-cycle p95 is based on 30 CPU ticks/29 intervals, not a pooled p95. No physical device performance claim.'}
for index in range(3):
 group=[r for r in rows if r['pass']==index];assert len(group)==21
 assert {r['replacement'] for r in group}==set(data['models'])
 summary['passes'].append({'pass':index,'size':[group[0]['width'],group[0]['height']],
   'loadMeanMs':statistics.mean(r['loadMs'] for r in group),'replaceMeanMs':statistics.mean(r['replaceMs'] for r in group),
   'tickMeanMs':statistics.mean(r['tickMeanMs'] for r in group),'worstCycleTickP95Ms':max(r['tickP95Ms'] for r in group),
   'frameMeanMs':statistics.mean(r['frameMeanMs'] for r in group),'worstCycleFrameP95Ms':max(r['frameP95Ms'] for r in group),
   'allocatedMiBRange':[min(r['allocated'] for r in group)/2**20,max(r['allocated'] for r in group)/2**20],
   'reservedMiBRange':[min(r['reserved'] for r in group)/2**20,max(r['reserved'] for r in group)/2**20],
   'managedWithGarbageMiBRange':[min(r['managedWithGarbage'] for r in group)/2**20,max(r['managedWithGarbage'] for r in group)/2**20]})
for name in data['models']:
 path=directory/(name+'.png')
 with Image.open(path) as image:
  assert image.size==(512,768)
  alpha=image.convert('RGBA').getchannel('A');active=sum(alpha.histogram()[1:]);assert active>1000,(name,active)
  summary['frames'][name]={'activePixels':active,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
(directory/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
print(json.dumps({k:v for k,v in summary.items() if k!='frames'},indent=2))
