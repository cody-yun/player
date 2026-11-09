"""Sustained generated face + PCM + renderer on a normal Mac player window; no hardware capture."""
from pathlib import Path
import argparse,hashlib,json,subprocess,time
from macos_player import resolve_app,launch,audit
p=argparse.ArgumentParser();p.add_argument('--seconds',type=int,default=600);p.add_argument('--phases',action='store_true');args=p.parse_args()
if not 30<=args.seconds<=7200:p.error('--seconds must be between 30 and 7200')
root=Path(__file__).resolve().parents[2];app,binary,_=resolve_app('arm64');bundle=audit(app,'arm64')
output=root/'unity/TestResults/macos-sustained'/str(time.time_ns());output.mkdir(parents=True)
command=[*launch(binary,'arm64'),'-screen-fullscreen','0','-screen-width','960','-screen-height','720',
         '-logFile',str(output/'player.log'),'-livlivSustained',str(output),'-livlivSustainedSeconds',str(args.seconds)]
if args.phases:command.append('-livlivSustainedPhases')
evidence={'result':'Running','scope':'Generated face inference + 650 Hz PCM analysis + renderer; no real camera/mic/output audio or forced GC; no universal performance/leak verdict',
          'frameTiming':'Wall-clock intervals between observed Update frame-counter changes, including diagnostic overhead; not GPU completion time',
          'command':command,'output':str(output),'osSamples':[],**bundle}
sources=['unity/Assets/Verification/PortVerification.Sustained.cs','unity/Assets/Verification/PortVerification.CaptureSoak.cs',
         'unity/Assets/Verification/PortVerification.SustainedPhases.cs',
         'unity/Assets/Verification/PortVerification.Native.cs','unity/Assets/Verification/PortVerification.cs',
         'unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs','scripts/unity/verify-macos-sustained.py']
evidence['sourceHashes']={s:hashlib.sha256((root/s).read_bytes()).hexdigest() for s in sources}
process=None;report=None
try:
    launched_at=time.monotonic()
    with (output/'launcher.log').open('w') as log:process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT)
    evidence['pid']=process.pid;print(json.dumps({'output':str(output),'pid':process.pid,'seconds':args.seconds}),flush=True)
    last=0;deadline=time.monotonic()+args.seconds+90
    while time.monotonic()<deadline:
        if process.poll() is not None:raise RuntimeError('Player exited: '+str(process.returncode))
        try:report=json.loads((output/'sustained.json').read_text())
        except (FileNotFoundError,json.JSONDecodeError):time.sleep(.2);continue
        count=len(report.get('samples') or [])
        if count!=last:
            # Terminal active samples may be published in the same frame. Preserve all Unity
            # rows; OS measurements occur when observed, not retrospectively at Unity time.
            for index in range(last,count):
                sample=report['samples'][index]
                if sample['measuredFrames']:
                    previous=report['samples'][index-1]
                    wall=sample['elapsed']-previous['elapsed']
                    measured=sample['meanFrameMs']*sample['measuredFrames']/1000
                    if abs(wall-measured)>.1:raise RuntimeError('Frame timing does not account for wall time')
                    if sample['player']['frames']-previous['player']['frames']!=sample['measuredFrames']:raise RuntimeError('Frame observation skipped an Update')
                    if args.phases:
                        m=report.get('phaseMaxima') or {}
                        if not report.get('phaseTimings') or any(m.get(k,0)<=0 for k in ['updateMs','tickMs','reportMs','faceInclusiveMs','sourceMs','voiceMs']):
                            raise RuntimeError('Missing active phase timing measurements')
                        for child,parent in [('sourceMs','faceInclusiveMs'),('faceInclusiveMs','tickMs'),('voiceMs','tickMs'),('tickMs','updateMs'),('reportMs','updateMs')]:
                            if m[child]>m[parent]+.05:raise RuntimeError('Nested phase timing exceeds its parent')
                stats=subprocess.check_output(['ps','-p',str(process.pid),'-o','rss=','-o','vsz='],text=True,timeout=5).split()
                row={'index':index,'stage':sample['stage'],'elapsed':sample['elapsed'],'rssKiB':int(stats[0]),'virtualKiB':int(stats[1]),
                     'hostElapsedSeconds':time.monotonic()-launched_at,'rowsInObservedReport':count}
                evidence['osSamples'].append(row)
                print(json.dumps({**row,'frames':sample['player']['frames'],'faceFps':sample['player']['faceFps'],'p95FrameMs':sample['p95FrameMs']}),flush=True)
            last=count
            (output/'os-samples.json').write_text(json.dumps(evidence['osSamples'],indent=2)+'\n')
        if report['result']!='Running':break
        time.sleep(.2)
    else:raise TimeoutError('Sustained run deadline exceeded')
    if report['result']!='Passed' or not report['sourceReleased'] or not report['voiceReleased']:raise RuntimeError(report.get('error') or 'Input cleanup failed')
    active=[s for s in report['samples'] if s['stage'].startswith('active')]
    if active[-1]['elapsed']-active[0]['elapsed']<args.seconds:raise RuntimeError('Active duration was shortened')
    if any(s['player']['scale']!=1 for s in report['samples']):raise RuntimeError('Scale changed')
    stopped=report['samples'][-1];baseline=report['samples'][0]
    if stopped['stage']!='stopped' or stopped['player']['tracking'] or stopped['player']['microphone']:raise RuntimeError('Inputs remained active')
    if any(stopped['memory'][k]!=baseline['memory'][k] for k in ['cameras','clips','textures']):raise RuntimeError('Capture/texture objects did not return to baseline')
    evidence['result']='Passed'
except BaseException as error:evidence['result']='Failed';evidence['error']=str(error)
finally:
    evidence['unity']=report
    if process is not None and process.poll() is None:
        process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait()
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'result':evidence['result'],'output':str(output),'error':evidence.get('error')}),flush=True)
if evidence['result']!='Passed':raise SystemExit(1)
