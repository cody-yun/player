"""Thirty actual Mac capture reopen cycles, synchronized Unity and process memory.

Normal windowed loop. No raw video/audio is saved. Only the final diagnostic sample forces GC.
"""
from pathlib import Path
import argparse,hashlib,json,subprocess,time
from macos_player import resolve_app,launch,audit
parser=argparse.ArgumentParser();parser.add_argument('--architecture',choices=['arm64','x86_64'],default='arm64')
parser.add_argument('--inference-only',action='store_true',help='Use generated frames; no camera/microphone capture');args=parser.parse_args()
root=Path(__file__).resolve().parents[2];app,binary,plist=resolve_app(args.architecture);bundle=audit(app,args.architecture)
category='macos-inference-soak' if args.inference_only else 'macos-capture-soak'
output=root/'unity/TestResults'/category/str(time.time_ns());output.mkdir(parents=True)
command=[*launch(binary,args.architecture),'-screen-fullscreen','0','-screen-width','960','-screen-height','720',
         '-logFile',str(output/'player.log'),'-livlivInferenceSoak' if args.inference_only else '-livlivCaptureSoak',str(output)]
evidence={'result':'Failed','scope':'Thirty live macOS camera/microphone cycles; normal windowed loop; RSS is not Android PSS or a whole-process leak verdict',
          'inferenceOnly':args.inference_only,'command':command,'output':str(output),'osSamples':[],**bundle}
if args.inference_only:evidence['scope']='Thirty generated portrait MediaPipe task/image cycles; no camera/microphone capture; RSS is not a whole-process leak verdict'
process=None
try:
    with (output/'launcher.log').open('w') as log:process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT)
    print(json.dumps({'pid':process.pid,'output':str(output)}),flush=True)
    deadline=time.monotonic()+600;last=0;report=None
    while time.monotonic()<deadline:
        if process.poll() is not None:raise RuntimeError('Player exited before completion: '+str(process.returncode))
        try:report=json.loads((output/'capture-soak.json').read_text())
        except (FileNotFoundError,json.JSONDecodeError):time.sleep(.1);continue
        count=len(report.get('samples') or [])
        if count!=last:
            if count!=last+1:raise RuntimeError('Skipped a synchronized memory observation')
            sample=report['samples'][-1]
            memory=subprocess.check_output(['ps','-p',str(process.pid),'-o','rss=','-o','vsz='],text=True,timeout=5).split()
            threads=subprocess.check_output(['ps','-M','-p',str(process.pid)],text=True,timeout=5)
            # ps -M uses its own header/row layout on macOS, ignoring custom -o formatting.
            row={'sampleIndex':count-1,'cycle':sample['cycle'],'stage':sample['stage'],
                 'rssKiB':int(memory[0]),'virtualKiB':int(memory[1]),'psThreadRows':len([s for s in threads.splitlines() if s.strip()])-1,
                 'observedAt':time.monotonic()}
            evidence['osSamples'].append(row);(output/'os-samples.json').write_text(json.dumps(evidence['osSamples'],indent=2)+'\n')
            (output/('ack-'+str(count))).touch();last=count
            print(json.dumps({**row,'cameras':sample['cameras'],'clips':sample['clips'],'allocated':sample['allocated']}),flush=True)
        if report['result']!='Running':break
        time.sleep(.1)
    else:raise TimeoutError('Capture soak deadline exceeded')
    evidence['unity']=report
    if report['result']!='Passed' or not report['captureObjectsReturned'] or not report['renderObjectsStableAfterWarmup']:
        raise RuntimeError(report.get('error') or 'Capture resource checks failed')
    if report['cycles']!=30 or len(report['samples'])!=63 or len(evidence['osSamples'])!=63:raise RuntimeError('Incomplete observation set')
    if any(s['scale']!=1 for s in report['samples']):raise RuntimeError('Character scale changed')
    evidence['result']='Passed'
except Exception as error:evidence['error']=str(error)
finally:
    if process is not None and process.poll() is None:
        process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait()
    names=['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.CaptureSoak.cs',
           'unity/Assets/Verification/PortVerification.InferenceSoak.cs',
           'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/MicrophoneInput.cs',
           'unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs','unity/Packages/ai.livliv.mediapipe/Runtime/WebCamFaceSource.cs']
    evidence['sourceHashes']={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in names}
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'result':evidence['result'],'output':str(output),'error':evidence.get('error')}),flush=True)
if evidence['result']!='Passed':raise SystemExit(1)
