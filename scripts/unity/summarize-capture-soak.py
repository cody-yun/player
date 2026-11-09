"""Summarize observed memory, without treating allocator reservation or raw PSS as a leak verdict."""
from pathlib import Path
import argparse,json,statistics
p=argparse.ArgumentParser();p.add_argument('directory',type=Path);a=p.parse_args()
r=json.loads((a.directory/'evidence.json').read_text());u=r['unity'];os={s['sampleIndex']:s for s in r['osSamples']}
resident='pssKiB' if 'pssKiB' in os[0] else 'rssKiB'
thread_key='threads' if 'threads' in os[0] else 'psThreadRows'
rows=[]
for index,s in enumerate(u['samples']):rows.append({**s,**{k:v for k,v in os[index].items() if k in [resident,thread_key]}})
stopped=[s for s in rows if s['stage']=='stopped'];steady=[s for s in stopped if s['cycle']>=u['warmupCycles']]
def metric(key):
    xs=[s['cycle'] for s in steady];ys=[s[key] for s in steady]
    xm=statistics.mean(xs);ym=statistics.mean(ys)
    tx=xs[-10:];ty=ys[-10:];txm=statistics.mean(tx);tym=statistics.mean(ty)
    return {'unit':'KiB' if key==resident else 'count' if key==thread_key else 'bytes',
            'steadyMin':min(ys),'steadyMax':max(ys),'firstFiveStoppedMean':statistics.mean(s[key] for s in stopped[:5]),
            'lastFiveStoppedMean':statistics.mean(s[key] for s in stopped[-5:]),
            'steadySlopePerCycle':sum((x-xm)*(y-ym) for x,y in zip(xs,ys))/sum((x-xm)**2 for x in xs),
            'lastTenMin':min(ty),'lastTenMax':max(ty),
            'lastTenSlopePerCycle':sum((x-txm)*(y-tym) for x,y in zip(tx,ty))/sum((x-txm)**2 for x in tx),
            'idleBeforeGc':next(s[key] for s in rows if s['stage']=='idle-before-gc'),
            'afterExplicitGc':next(s[key] for s in rows if s['stage']=='after-explicit-gc')}
result={'result':r['result'],'cycles':u['cycles'],'warmupCycles':u['warmupCycles'],
        'seconds':rows[-1]['time']-rows[0]['time'],'apkSha256':r.get('apkSha256'),'residentMetric':resident,
        'captureObjectsReturned':u['captureObjectsReturned'],'renderObjectsStableAfterWarmup':u['renderObjectsStableAfterWarmup'],
        'metrics':{k:metric(k) for k in [resident,'allocated','reserved','managedWithGarbage',thread_key]},
        'baselineObjects':{k:rows[0][k] for k in ['cameras','clips','textures','targets','materials','meshes']},
        'lastStoppedObjects':{k:stopped[-1][k] for k in ['cameras','clips','textures','targets','materials','meshes']}}
(a.directory/'summary.json').write_text(json.dumps(result,indent=2)+'\n');print(json.dumps(result,indent=2))
