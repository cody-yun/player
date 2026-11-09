"""Summarize completed wall-clock endurance evidence; never classify it as leak-free."""
from pathlib import Path
import argparse,json,statistics

p=argparse.ArgumentParser();p.add_argument('directory',type=Path);p.add_argument('--plot',action='store_true');a=p.parse_args()
d=json.loads((a.directory/'evidence.json').read_text());u=d['unity']
if d['result']!='Passed' or not d.get('frameTiming','').startswith('Wall-clock'):
    raise ValueError('Requires a completed wall-clock run; preliminary Unity delta-time measurements are not performance evidence')
rows=u['samples'];os_rows={r['index']:r for r in d['osSamples']}
active=[(i,r) for i,r in enumerate(rows) if r['stage'].startswith('active')]
measured=[r for _,r in active if r['measuredFrames']]
seconds=active[-1][1]['elapsed']-active[0][1]['elapsed']
count=sum(r['measuredFrames'] for r in measured)
total_ms=sum(r['meanFrameMs']*r['measuredFrames'] for r in measured)
if seconds<u['requestedSeconds'] or abs(total_ms/1000-seconds)>.15:
    raise ValueError('Frame timing does not cover the requested wall duration')
if len(os_rows)!=len(rows):raise ValueError('Missing OS observations')
# Use the regular active samples for trends; an immediate active-final row would double-weight the end.
regular=[(i,r) for i,r in active if r['stage']=='active']
origin=regular[0][1]['elapsed']
def metric(read):
    points=[(r['elapsed']-origin,read(i,r)) for i,r in regular]
    steady=[v for v in points if v[0]>=60]
    if len(steady)<2:steady=points
    xs,ys=zip(*steady);xm=statistics.mean(xs);ym=statistics.mean(ys)
    slope=sum((x-xm)*(y-ym) for x,y in steady)/sum((x-xm)**2 for x in xs)
    late=[v for v in points if v[0]>=max(0,seconds-300)]
    if len(late)<2:late=points
    lx,ly=zip(*late);lmx=statistics.mean(lx);lmy=statistics.mean(ly)
    late_slope=sum((x-lmx)*(y-lmy) for x,y in late)/sum((x-lmx)**2 for x in lx)
    return {'activeMinMiB':min(y for _,y in points),'activeMaxMiB':max(y for _,y in points),
            'firstFiveMeanMiB':statistics.mean(y for _,y in points[:5]),'lastFiveMeanMiB':statistics.mean(y for _,y in points[-5:]),
            'afterFirstMinuteSlopeMiBPerMinute':slope*60,'trendSamples':len(steady),
            'lastFiveMinutesSlopeMiBPerMinute':late_slope*60,'lastFiveMinutesMinMiB':min(ly),'lastFiveMinutesMaxMiB':max(ly)}
summary={'result':'Passed','scope':d['scope'],'requestedSeconds':u['requestedSeconds'],'activeSeconds':seconds,
         'hardware':{k:u[k] for k in ['device','processor','operatingSystem','systemMemoryMiB','renderWidth','renderHeight','screenWidth','screenHeight']},
         'frameTiming':{'source':d['frameTiming'],'measuredFrames':count,'weightedMeanMs':total_ms/count,
                        'observedFramesPerSecond':count/seconds,'intervalP95MinMs':min(r['p95FrameMs'] for r in measured),
                        'intervalP95MaxMs':max(r['p95FrameMs'] for r in measured),'worstIntervalMs':max(r['maxFrameMs'] for r in measured),
                        'note':'The range is of per-interval p95 values, not a pooled p95 or GPU timing'},
         'sampledFaceFpsRange':[min(r['player']['faceFps'] for _,r in regular),max(r['player']['faceFps'] for _,r in regular)],
         'faceSeenAtEveryActiveSample':all(r['player']['faceSeen'] for _,r in active),
         'resourcesReleased':u['sourceReleased'] and u['voiceReleased'],
         'memory':{'rss':metric(lambda i,r:os_rows[i]['rssKiB']/1024),
                   **{k:metric(lambda i,r,key=k:r['memory'][key]/2**20) for k in ['allocated','reserved','managedWithGarbage']}},
         'limits':'Single desktop model/run; no hardware capture/audio output, physical mobile, thermals, battery or whole-process leak verdict'}
if u.get('phaseTimings'):
    gaps=u.get('longFrames') or []
    detailed=[]
    for gap in sorted(gaps,key=lambda g:g['wallGapMs'],reverse=True)[:20]:
        t=gap['timing'];diagnostic=gap.get('diagnosticSaveMs',0)
        regions={'sourceRead':t['sourceMs'],'faceRemainder':t['faceInclusiveMs']-t['sourceMs'],
                 'voiceRead':t['voiceMs'],'otherPlayerTick':t['tickMs']-t['faceInclusiveMs']-t['voiceMs'],
                 'report':t['reportMs'],'otherSampleUpdate':t['updateMs']-t['tickMs']-t['reportMs'],
                 'diagnosticSave':diagnostic,'outsideTimedRegions':gap['wallGapMs']-t['updateMs']-diagnostic}
        detailed.append({**gap,'nonOverlappingRegionsMs':regions})
    summary['phases']={'independentMaximaMs':u.get('phaseMaxima'),'loggedLongFrames':len(gaps),'largestWallGaps':detailed,
                       'limits':'Elapsed wall spans include preemption and GC; faceRemainder also includes adapter bookkeeping. OtherPlayerTick includes composition/physics/deformation/render submission, not GPU completion. OutsideTimedRegions includes idle/pacing/scheduling and unmeasured engine work. Independent maxima need not share a frame; GC counters do not measure pause duration.'}
with (a.directory/'summary.json').open('x') as f:json.dump(summary,f,indent=2);f.write('\n')
if a.plot:
    import matplotlib
    matplotlib.use('Agg')
    import matplotlib.pyplot as plt
    path=a.directory/'sustained-trend.png'
    if path.exists():raise FileExistsError(path)
    ts=[r['elapsed']-origin for _,r in regular]
    fig,axes=plt.subplots(4,1,figsize=(10,10),sharex=True,layout='constrained')
    axes[0].plot(ts,[os_rows[i]['rssKiB']/1024 for i,_ in regular],'.-',color='#3465ba');axes[0].set_ylabel('Process RSS (MiB)')
    for key,label in [('allocated','Unity used'),('reserved','Unity reserved')]:
        axes[1].plot(ts,[r['memory'][key]/2**20 for _,r in regular],label=label)
    axes[1].set_ylabel('Unity memory (MiB)');axes[1].legend(fontsize=8)
    axes[2].plot(ts,[r['memory']['managedWithGarbage']/2**20 for _,r in regular],color='#87549b');axes[2].set_ylabel('Managed incl. garbage (MiB)')
    for key,label in [('meanFrameMs','Interval mean'),('p95FrameMs','Interval p95')]:
        axes[3].plot([r['elapsed']-origin for r in measured],[r[key] for r in measured],label=label)
    axes[3].set_ylabel('Wall frame interval (ms)');axes[3].legend(fontsize=8);axes[3].set_xlabel('Active elapsed seconds')
    axes[3].set_title('Worst observed gap: %.2f ms (mean/p95 curves below)'%summary['frameTiming']['worstIntervalMs'],fontsize=10,loc='left')
    for ax in axes:ax.grid(alpha=.2)
    fig.suptitle('Generated face + PCM + render: sustained macOS run\nNo forced GC; bounded evidence, no leak-free or universal performance claim',fontsize=12)
    fig.savefig(path,dpi=150);plt.close(fig)
print(json.dumps(summary,indent=2))
