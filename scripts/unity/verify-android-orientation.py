"""Run explicit Unity orientation requests in BlueStacks; restore the ordinary sample.
Sensor auto-rotation and physical devices are outside this probe. No inputs are opened.
"""
from pathlib import Path
import hashlib,json,struct,subprocess,time
root=Path(__file__).resolve().parents[2]
adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb'
package='ai.livliv.verification';serial='127.0.0.1:5555';component=package+'/com.unity3d.player.UnityPlayerGameActivity'
stamp=str(time.time_ns());output=root/'unity/TestResults/android-orientation'/stamp;output.mkdir(parents=True)
remote='/sdcard/Android/data/'+package+'/files/orientation-'+stamp
def run(*args):return subprocess.check_output([adb,'-s',serial,*map(str,args)],text=True,stderr=subprocess.STDOUT,timeout=40)
subprocess.run([adb,'connect',serial],check=True)
apk=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',apk)
try:
    command=['shell',f'am start -S -W -n {component} --es unity "-livlivOrientationProbe {remote}"']
    (output/'launch.log').write_text(run(*command))
    deadline=time.monotonic()+90;captured={}
    while time.monotonic()<deadline:
        for name in ['Portrait','LandscapeLeft','PortraitUpsideDown','LandscapeRight']:
            if name in captured:continue
            try:marker=json.loads(run('shell','cat',remote+'/'+name+'.phase.json'))
            except (subprocess.CalledProcessError,json.JSONDecodeError):continue
            image=subprocess.check_output([adb,'-s',serial,'exec-out','screencap','-p'],timeout=30)
            assert image[:8]==b'\x89PNG\r\n\x1a\n'
            layout=marker['player']['layout']
            assert struct.unpack('>II',image[16:24])==(layout['width'],layout['height']),'Android capture did not match the marked Unity surface'
            (output/(name+'.adb.png')).write_bytes(image);captured[name]=marker
        try:report=json.loads(run('shell','cat',remote+'/orientation.json'));break
        except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(.5)
    else:raise TimeoutError('Orientation probe did not produce a report')
    run('pull',remote+'/.',output)
    assert len(captured)==4,'Android screen captures did not cover every phase'
    assert len(report['phases'])==4,report
    assert report['autoRotationRestored'] is True
    verified=[];unmet=[]
    for phase in report['phases']:
        if phase.get('error'):
            unmet.append({'requested':phase['requested'],'error':phase['error'],'observed':phase['player']['layout']});continue
        state=phase['player'];layout=state['layout'];image=(output/phase['screenshot']).read_bytes()
        assert image[:8]==b'\x89PNG\r\n\x1a\n'
        assert struct.unpack('>II',image[16:24])==(layout['width'],layout['height'])
        assert layout['visibleButtons']==6 and layout['clippedButtons']==layout['overlappingPairs']==layout['scrollComponents']==0
        assert layout['orientation']==phase['requested'] and state['status']=='Ready' and state['scale']==1 and not state['tracking'] and not state['microphone']
        verified.append(phase['requested'])
    assert (report['result']=='Passed')==(len(unmet)==0),report
    evidence={'result':report['result'],'verifiedOrientations':verified,'unmetOrientations':unmet,'scope':report['scope'],'visualReview':'Pending','report':report,'androidCaptureMarkers':captured,'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),
              'sourceHashes':{str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [root/'unity/Assets/Verification'/n for n in ['PortVerification.cs','PortVerification.Orientation.cs','VerificationHud.cs']]},
              'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(output.iterdir()) if p.is_file()}}
    (output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
    print(json.dumps({'result':report['result'],'verified':verified,'unmet':[p['requested'] for p in unmet],'output':str(output),'visualReview':'Pending'},indent=2))
finally:
    with (output/'restore-startup.log').open('w') as stream:
        subprocess.run(['python3',str(root/'scripts/unity/verify-android-startup.py')],stdout=stream,stderr=subprocess.STDOUT,check=True)
evidence['restoreStartupLogSha256']=hashlib.sha256((output/'restore-startup.log').read_bytes()).hexdigest()
(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
if evidence['result']!='Passed':raise SystemExit(1)
