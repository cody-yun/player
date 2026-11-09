"""Render local QA gallery and measurement plots from saved evidence; requires matplotlib/Pillow."""
from pathlib import Path
from PIL import Image
import argparse,json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

parser=argparse.ArgumentParser();parser.add_argument('directory',type=Path);args=parser.parse_args();directory=args.directory
data=json.loads((directory/'evidence.json').read_text());names=sorted(data['models']);rows=data['rows']
fig,axes=plt.subplots(3,7,figsize=(14,10))
for ax,name in zip(axes.flat,names):
 with Image.open(directory/(name+'.png')) as image:ax.imshow(image.convert('RGBA'))
 ax.set_title(name,fontsize=10);ax.axis('off')
fig.suptitle(data['platform']+' / '+data['graphics']+' — 21 saved model frames\nGenerated motion; rendering coverage, not upstream pixel parity',fontsize=12)
fig.tight_layout();fig.savefig(directory/'model-gallery.png',dpi=140);plt.close(fig)

fig,axes=plt.subplots(2,1,figsize=(12,7),sharex=True);x=list(range(1,len(rows)+1))
axes[0].plot(x,[r['tickMeanMs'] for r in rows],label='Tick mean (CPU elapsed incl. submission)')
axes[0].plot(x,[r['tickP95Ms'] for r in rows],label='Tick p95 within each 30-frame cycle',alpha=.75)
axes[0].set_ylabel('Milliseconds');axes[0].legend(fontsize=8);axes[0].grid(alpha=.2)
axes[1].plot(x,[r['allocated']/2**20 for r in rows],label='Unity allocated after disposal')
axes[1].plot(x,[r['reserved']/2**20 for r in rows],label='Unity reserved after disposal')
axes[1].plot(x,[r['managedWithGarbage']/2**20 for r in rows],label='Managed incl. garbage')
axes[1].set_ylabel('MiB');axes[1].set_xlabel('Load / replace / animate / dispose cycle');axes[1].legend(fontsize=8);axes[1].grid(alpha=.2)
for ax in axes:
 for boundary in [21.5,42.5]:ax.axvline(boundary,color='grey',linestyle='--',linewidth=.8)
fig.suptitle(data['platform']+' / '+data['graphics']+' — bounded model lifecycle probe\n512×768 → 320×480 → 512×768; no forced GC or hardware capture',fontsize=12)
fig.tight_layout();fig.savefig(directory/'model-timing-memory.png',dpi=140);plt.close(fig)
print(directory/'model-gallery.png');print(directory/'model-timing-memory.png')
