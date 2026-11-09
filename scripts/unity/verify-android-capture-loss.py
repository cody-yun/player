"""BlueStacks active-input cleanup after explicit native context-loss notification."""
from pathlib import Path
import argparse,hashlib,json,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('--end',action='store_true',help='Stop Unity devices rather than notifying context loss');args=parser.parse_args()
probe='capture-end' if args.end else 'capture-loss';flag='-livlivCaptureEnd' if args.end else '-livlivCaptureLoss'
root=Path(__file__).resolve().parents[2]
adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
serial='127.0.0.1:5555';package='ai.livliv.verification'
component=package+'/com.unity3d.player.UnityPlayerGameActivity'
stamp=str(time.time_ns());output=root/'unity/TestResults/bluestacks'/probe/stamp
output.mkdir(parents=True);remote='/sdcard/Android/data/'+package+'/files/'+probe+'-'+stamp
def run(*args):return subprocess.check_output([adb,'-s',serial,*map(str,args)],text=True,stderr=subprocess.STDOUT,timeout=40)
subprocess.run([adb,'connect',serial],check=True)
apk=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',apk)
try:
    run('shell',f'am start -S -W -n {component} --es unity "{flag} {remote}"')
    pid=run('shell','pidof',package).strip();assert pid
    deadline=time.monotonic()+80
    while time.monotonic()<deadline:
        assert run('shell','pidof',package).strip()==pid,'App exited'
        try:report=json.loads(run('shell','cat',remote+'/'+probe+'.json'));break
        except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(.5)
    else:raise TimeoutError('Capture loss report missing')
    run('pull',remote+'/.',output)
    (output/'player.log').write_text(run('logcat','-d','--pid='+pid))
    ops={op:run('shell','cmd','appops','get',package,op) for op in ['CAMERA','RECORD_AUDIO']}
    sources=['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.CaptureSoak.cs',
             'unity/Assets/Verification/PortVerification.CaptureEnd.cs','unity/Packages/ai.livliv.player/Runtime/Platform/ICaptureStatus.cs',
             'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/MicrophoneInput.cs',
             'unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs','unity/Packages/ai.livliv.mediapipe/Runtime/WebCamFaceSource.cs']
    scope='Unity device Stop/End with active BlueStacks capture; no physical unplug' if args.end else 'Explicit native notification with active BlueStacks camera/microphone; no driver reset'
    evidence={'scope':scope,
              'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),'report':report,'appops':ops,
              'sourceHashes':{name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in sources}}
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    assert report['result']=='Passed' and report['graphics']=='OpenGLES3',report
    assert all('running' not in value.lower() for value in ops.values()),ops
    print(json.dumps({'result':'Passed','output':str(output)},indent=2))
finally:
    run('shell','am','start','-S','-W','-n',component)
