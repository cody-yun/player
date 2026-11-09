"""Export measured capture-soak memory trends. Requires matplotlib."""
from pathlib import Path
import argparse,json
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);a=p.parse_args()
r=json.loads((a.directory/'evidence.json').read_text());rows=r['unity']['samples'];os={s['sampleIndex']:s for s in r['osSamples']}
resident='pssKiB' if 'pssKiB' in os[0] else 'rssKiB'
inference_only=r.get('inferenceOnly',False)
platform_label='BlueStacks ARM64' if resident=='pssKiB' else 'macOS ARM64'
cycle_label='generated portrait inference task cycles' if inference_only else 'live capture start/stop cycles'
origin=rows[0]['time'];xs=[s['time']-origin for s in rows]
fig,axes=plt.subplots(3,1,figsize=(10,8),sharex=True,layout='constrained')
for stage,color,label in [('active','#228c77','Active inference task' if inference_only else 'Active camera + microphone'),('stopped','#3465ba','After task disposal' if inference_only else 'After Stop')]:
    ids=[i for i,s in enumerate(rows) if s['stage']==stage]
    axes[0].plot([xs[i] for i in ids],[os[i][resident]/1024 for i in ids],'.-',color=color,label=label)
axes[0].set_ylabel(('Android PSS' if resident=='pssKiB' else 'Process RSS')+' (MiB)');axes[0].legend(loc='lower right',fontsize=8)
axes[1].plot(xs,[s['allocated']/2**20 for s in rows],'.-',label='Unity used allocation',color='#3465ba')
axes[1].plot(xs,[s['reserved']/2**20 for s in rows],label='Unity reserved pools',color='#ad7a22')
axes[1].set_ylabel('Unity memory (MiB)');axes[1].legend(fontsize=8)
axes[2].plot(xs,[s['managedWithGarbage']/2**20 for s in rows],'.-',color='#87549b')
axes[2].set_ylabel('Managed incl. garbage (MiB)');axes[2].set_xlabel('Elapsed seconds (host sampling waits included)')
gc=next(i for i,s in enumerate(rows) if s['stage']=='after-explicit-gc')
for ax in axes:
    ax.axvline(xs[gc],color='#b84137',linestyle='--',linewidth=1);ax.grid(alpha=.2)
axes[2].annotate('Explicit diagnostic GC',xy=(xs[gc],rows[gc]['managedWithGarbage']/2**20),xytext=(-155,32),textcoords='offset points',arrowprops={'arrowstyle':'->','color':'#b84137'},fontsize=9)
fig.suptitle(platform_label+': 30 '+cycle_label+'\nBounded object-recovery test; resident-memory growth is not classified as leak-free',fontsize=12)
fig.savefig(a.directory/'memory-trend.png',dpi=160);plt.close(fig)
print(a.directory/'memory-trend.png')
