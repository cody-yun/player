"""30 real camera/microphone reopen cycles, with separate Unity and Android memory evidence.

Requires the authorized BlueStacks session and already granted capture permissions.
The only forced GC occurs at the explicitly labelled final diagnostic sample.
"""
from pathlib import Path
import hashlib,json,re,subprocess,time
root=Path(__file__).resolve().parents[2]
adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
serial='127.0.0.1:5555';package='ai.livliv.verification'
component=package+'/com.unity3d.player.UnityPlayerGameActivity';stamp=str(time.time_ns())
output=root/'unity/TestResults/bluestacks/capture-soak'/stamp;output.mkdir(parents=True)
remote='/sdcard/Android/data/'+package+'/files/capture-soak-'+stamp
apk=root/'unity/TestResults/Android/LivLivVerification.apk';pid=None;observations=[]
def run(*args):return subprocess.check_output([adb,'-s',serial,*map(str,args)],text=True,stderr=subprocess.STDOUT,timeout=30)
def restore():run('shell',f'am start -S -W -n {component} ')
subprocess.run([adb,'connect',serial],check=True)
try:
    run('install','-r',apk)
    run('shell',f'am start -S -W -n {component} --es unity "-livlivCaptureSoak {remote}"')
    pid=run('shell','pidof',package).strip();assert pid
    deadline=time.monotonic()+600;last=-1
    while time.monotonic()<deadline:
        assert run('shell','pidof',package).strip()==pid,'App exited/restarted during soak'
        try:report=json.loads(run('shell','cat',remote+'/capture-soak.json'))
        except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(.5);continue
        count=len(report.get('samples') or [])
        if count!=last:
            last=count;sample=report['samples'][-1]
            memory=run('shell','dumpsys','meminfo',pid)
            status=run('shell','cat','/proc/'+pid+'/status')
            (output/f'{count:03}-meminfo.txt').write_text(memory)
            (output/f'{count:03}-status.txt').write_text(status)
            pss=re.search(r'TOTAL PSS:\s+(\d+)',memory);threads=re.search(r'^Threads:\s+(\d+)',status,re.M)
            row={'sampleIndex':count-1,'cycle':sample['cycle'],'stage':sample['stage'],'pssKiB':int(pss[1]) if pss else None,
                 'threads':int(threads[1]) if threads else None,'observedAt':time.monotonic()}
            row['appops']={op:run('shell','cmd','appops','get',package,op).strip() for op in ['CAMERA','RECORD_AUDIO']}
            observations.append(row);(output/'os-samples.json').write_text(json.dumps(observations,indent=2)+'\n')
            print(json.dumps({k:v for k,v in {**row,'allocated':sample['allocated'],'managedWithGarbage':sample['managedWithGarbage']}.items() if k!='appops'}),flush=True)
            run('shell','touch',remote+'/ack-'+str(count))
        if report['result']!='Running':break
        time.sleep(.5)
    else:raise TimeoutError('Capture soak did not finish')
    run('pull',remote+'/.',output)
    log=run('logcat','-d','--pid='+pid,'-v','brief','-s','Unity:I');(output/'player.log').write_text(log)
    ops={op:run('shell','cmd','appops','get',package,op) for op in ['CAMERA','RECORD_AUDIO']}
    evidence={'result':report['result'],'pid':pid,'output':str(output),'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),
              'sourceHashes':{},'appops':ops,'unity':report,'osSamples':observations,
              'unavailable':['File descriptor count: emulator denies /proc/PID/fd; run-as setegid is unavailable.']}
    for name in ['unity/Assets/Verification/PortVerification.CaptureSoak.cs','unity/Assets/Verification/PortVerification.cs',
                 'unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs','unity/Packages/ai.livliv.mediapipe/Runtime/WebCamFaceSource.cs',
                 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/MicrophoneInput.cs']:
        evidence['sourceHashes'][name]=hashlib.sha256((root/name).read_bytes()).hexdigest()
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    assert report['result']=='Passed' and report['captureObjectsReturned'] and report['renderObjectsStableAfterWarmup'],report.get('error')
    assert len(report['samples'])==63 and report['cycles']==30
    assert all('running' not in value.lower() for value in ops.values()),ops
    assert len(observations)==63,'Missing a synchronized OS sample'
    for row in observations:
        assert all(('running' in value.lower())==(row['stage']=='active') for value in row['appops'].values()),row
    print('PASSED '+str(output),flush=True)
finally:
    if pid:restore()
