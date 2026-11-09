"""Actual Mac camera/microphone start, independent stop/restart and cleanup.

May require the user's macOS camera/microphone permission. No raw capture is saved.
Only the launched verification player is terminated after input cleanup/report collection.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess,time
from macos_player import resolve_app,launch,audit
parser=argparse.ArgumentParser();parser.add_argument('--architecture',choices=['arm64','x86_64'],default='arm64')
parser.add_argument('--interactive',action='store_true',help='Use the normal windowed loop rather than batchmode')
parser.add_argument('--context-loss',action='store_true',help='Send an explicit context-loss notification while capturing');args=parser.parse_args()
root=Path(__file__).resolve().parents[2];app,binary,plist=resolve_app(args.architecture);bundle=audit(app,args.architecture)
probe='capture-loss' if args.context_loss else 'capture-end';flag='-livlivCaptureLoss' if args.context_loss else '-livlivCaptureEnd'
output=root/'unity/TestResults'/('macos-'+probe)/str(time.time_ns());output.mkdir(parents=True)
mode=['-screen-fullscreen','0','-screen-width','960','-screen-height','720'] if args.interactive else ['-batchmode']
command=[*launch(binary,args.architecture),*mode,'-logFile',str(output/'player.log'),flag,str(output)]
evidence={'result':'Failed','scope':'Actual macOS capture and Unity Stop/End; no physical device unplug, raw video or PCM recording',
          'trigger':'explicit NotifyContextLost; not a GPU reset' if args.context_loss else 'Unity WebCamTexture.Stop/Microphone.End',
          'command':command,'interactive':args.interactive,'output':str(output),**bundle}
process=None;started=None
try:
    with (output/'launcher.log').open('w') as log:
        process=subprocess.Popen(command,stdout=log,stderr=subprocess.STDOUT)
        started=time.monotonic()
    print(json.dumps({'pid':process.pid,'output':str(output)},indent=2),flush=True)
    path=output/(probe+'.json');deadline=time.monotonic()+80
    while not path.exists():
        if process.poll() is not None:raise RuntimeError('Player exited before capture report: '+str(process.returncode))
        if time.monotonic()>deadline:raise TimeoutError('Capture report deadline exceeded; inspect OS permission and logs')
        time.sleep(.5)
    report=json.loads(path.read_text());evidence['report']=report
    evidence['elapsedToReportSeconds']=time.monotonic()-started
    if report['result']!='Passed' or report['graphics']!='Metal':raise RuntimeError(report.get('error') or 'Capture-end failed')
    keys=['activeBefore','lost','disposed','inputsStopped','restartRejected'] if args.context_loss else [
        'activeBefore','cameraStopped','microphonePreserved','cameraRestarted','microphoneStopped','cameraPreserved','microphoneRestarted','renderContinued']
    if not all(report.get(key) for key in keys):raise RuntimeError('Capture lifecycle report incomplete')
    if args.context_loss:
        if report.get('callbacks')!=1 or report['camerasBefore']!=report['camerasAfter'] or report['clipsBefore']!=report['clipsAfter']:
            raise RuntimeError('Loss notification/resource invariant failed')
    elif report['camerasAfter']!=0 or report['clipsAfter']!=0:raise RuntimeError('Capture resources remained allocated')
    evidence['result']='Passed'
except Exception as error:evidence['error']=str(error)
finally:
    if process is not None and process.poll() is None:
        process.terminate()
        try:process.wait(timeout=10)
        except subprocess.TimeoutExpired:process.kill();process.wait()
    names=['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.CaptureEnd.cs',
           'unity/Assets/Verification/PortVerification.CaptureSoak.cs',
           'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/CapturePermissions.cs',
           'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/MicrophoneInput.cs',
           'unity/Packages/ai.livliv.mediapipe/Runtime/WebCamFaceSource.cs','unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs']
    evidence['sourceHashes']={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in names}
    log_path=output/'player.log'
    if log_path.exists():
        rows=[json.loads(m.group(1)) for m in re.finditer(r'LIVLIV_EVIDENCE (\{[^\n]+\})',log_path.read_text())]
        active=[s for s in rows if s['tracking'] and s['microphone']]
        evidence['observations']={'count':len(rows),'bothActive':len(active),'faceSeenWhileBothActive':sum(bool(s['faceSeen']) for s in active),
                                  'peakFaceFps':max((s['faceFps'] for s in active),default=0),'scaleAlwaysOne':all(s['scale']==1 for s in rows),
                                  'lastFrame':rows[-1]['frames'] if rows else None,
                                  'finalInputsOff':bool(rows) and not rows[-1]['tracking'] and not rows[-1]['microphone']}
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'result':evidence['result'],'output':str(output),'error':evidence.get('error')},indent=2))
if evidence['result']!='Passed':raise SystemExit(1)
