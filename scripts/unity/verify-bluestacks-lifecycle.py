"""Exercise live camera + microphone through UGUI and Android Home/return.

Requires the authorized BlueStacks session with permissions already granted.
Uses real emulator camera/microphone forwarding, not generated input. Captures
only this app's rendered UI/logs; does not record raw camera video or audio.
"""
from pathlib import Path
import argparse,hashlib,json,re,struct,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('--orientation',choices=['Portrait','LandscapeLeft']);args=parser.parse_args()

root=Path(__file__).resolve().parents[2]
adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
serial='127.0.0.1:5555';package='ai.livliv.verification'
component=package+'/com.unity3d.player.UnityPlayerGameActivity'
output=root/'unity/TestResults/bluestacks/lifecycle'/str(time.time_ns());output.mkdir(parents=True)
apk=root/'unity/TestResults/Android/LivLivVerification.apk'
states=[];pid=None
print(json.dumps({'output':str(output)}),flush=True)
def run(*args):
    return subprocess.check_output([adb,'-s',serial,*map(str,args)],text=True,stderr=subprocess.STDOUT,timeout=45)
def launch(restart=False):
    # Android am joins Unity extras as one argument only when shell-quoted.
    command=f'am start {"-S " if restart else ""}-W -n {component}'
    if args.orientation:command+=' --es unity "-livlivUiOrientation '+args.orientation+'"'
    return run('shell',command)
def logs():return run('logcat','-d','--pid='+pid,'-v','brief','-s','Unity:I')
def last():
    rows=[line.split('LIVLIV_EVIDENCE ',1)[1] for line in logs().splitlines() if 'LIVLIV_EVIDENCE ' in line]
    return json.loads(rows[-1]) if rows else None
def layout_of(value):
    layout=value['layout']
    assert layout['visibleButtons']==6 and layout['clippedButtons']==layout['overlappingPairs']==layout['scrollComponents']==0,layout
    return layout
def tap_control(name):
    value=last();layout=layout_of(value)
    x,y=layout[name+'X'],layout[name+'Y']
    assert 0<x<layout['width'] and 0<y<layout['height'],(name,layout)
    run('shell','input','tap',x,y)
def wait(predicate,seconds=35):
    deadline=time.monotonic()+seconds
    while time.monotonic()<deadline:
        value=last()
        if value and predicate(value):return value
        time.sleep(.25)
    raise TimeoutError('Expected lifecycle state; latest='+str(last()))
def screenshot(name):
    raw=subprocess.check_output([adb,'-s',serial,'exec-out','screencap','-p'],timeout=20)
    (output/(name+'.png')).write_bytes(raw)
    return struct.unpack_from('>II',raw,16)
def appops():return {op:run('shell','cmd','appops','get',package,op).strip() for op in ['CAMERA','RECORD_AUDIO']}
def running(ops):return all('running' in value.lower() for value in ops.values())
def closed(ops):return all('running' not in value.lower() for value in ops.values())
def wait_ops(predicate):
    deadline=time.monotonic()+10
    while time.monotonic()<deadline:
        result=appops()
        if predicate(result):return result
        time.sleep(.25)
    raise AssertionError(('Android capture state',result))
def record(name,value,ops):
    assert run('shell','pidof',package).strip()==pid,'App restarted instead of resuming'
    assert value['scale']==1 and not value['error'] and not value['faceError'],value
    layout=layout_of(value)
    if args.orientation:assert layout['orientation']==args.orientation,(name,layout)
    info=run('shell','dumpsys','meminfo',pid)
    (output/(name+'-meminfo.txt')).write_text(info)
    row={'name':name,'pid':pid,'monotonic':time.monotonic(),'unity':value,'appops':ops}
    states.append(row);(output/'states.json').write_text(json.dumps(states,indent=2)+'\n')
    print(json.dumps({'name':name,'frames':value['frames'],'camera':value['tracking'],'microphone':value['microphone'],'scale':value['scale']}),flush=True)

subprocess.run([adb,'connect',serial],check=True)
try:
    run('install','-r',apk);launch(True);pid=run('shell','pidof',package).strip();assert pid
    baseline=wait(lambda v:v['status']=='Ready' and v['frames']>15 and (not args.orientation or v['layout']['orientation']==args.orientation))
    if args.orientation:assert (baseline['layout']['width']<baseline['layout']['height'])==(args.orientation=='Portrait')
    width,height=screenshot('initial');assert (width,height)==(baseline['layout']['width'],baseline['layout']['height'])
    record('initial',baseline,wait_ops(closed))
    for cycle in range(3):
        # Tap the actual UGUI geometry, including portrait layouts. Existing granted
        # permissions are prerequisites; this test does not dismiss permission dialogs.
        tap_control('camera')
        wait(lambda v:v['tracking'])
        tap_control('microphone')
        active=wait(lambda v:v['tracking'] and v['microphone'])
        active=wait(lambda v:v['tracking'] and v['microphone'] and v['frames']>active['frames']+60)
        record(f'{cycle}-active',active,wait_ops(running));screenshot(f'{cycle}-active')
        run('shell','input','keyevent','KEYCODE_HOME')
        background=wait(lambda v:not v['tracking'] and not v['microphone'])
        record(f'{cycle}-background',background,wait_ops(closed))
        time.sleep(2)
        launch()
        resumed=wait(lambda v:v['frames']>active['frames']+90 and not v['tracking'] and not v['microphone'])
        record(f'{cycle}-resumed',resumed,wait_ops(closed));screenshot(f'{cycle}-resumed')
    log=logs();(output/'player.log').write_text(log)
    assert log.count('LIVLIV_LIFECYCLE paused=True')>=3,log[-2000:]
    assert log.count('LIVLIV_LIFECYCLE paused=False')>=3,log[-2000:]
    sources=['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/VerificationHud.cs','unity/Packages/ai.livliv.player/Runtime/Platform/CaptureActivationGate.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/MicrophoneInput.cs','unity/Packages/ai.livliv.mediapipe/Runtime/WebCamFaceSource.cs']
    evidence={'result':'Passed','scope':'Three real Android Home/return cycles through responsive UGUI; live BlueStacks forwarded camera/microphone, not physical-mobile certification','requestedOrientation':args.orientation,'output':str(output),'cycles':3,'pid':pid,'states':states,
              'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),
              'sourceHashes':{f:hashlib.sha256((root/f).read_bytes()).hexdigest() for f in sources}}
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print('PASSED '+str(output),flush=True)
finally:
    # Always close live capture, even when a state check fails. Restart the
    # ordinary sample with inputs off; no permission or app data resets.
    if pid:
        try:(output/'final-player.log').write_text(logs())
        finally:
            run('shell','am','start','-S','-W','-n',component);pid=run('shell','pidof',package).strip();assert pid
            restored=wait(lambda v:v['status']=='Ready' and not v['tracking'] and not v['microphone'])
            ops=wait_ops(closed);layout_of(restored)
            (output/'restoration.json').write_text(json.dumps({'result':'Passed','pid':pid,'unity':restored,'appops':ops},indent=2)+'\n')
