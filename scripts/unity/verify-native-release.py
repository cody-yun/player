"""Run the logical target-release probe on Mac, authorized BlueStacks, or Docker/Mesa Linux."""
from pathlib import Path
import argparse,hashlib,json,subprocess,sys,time
p=argparse.ArgumentParser();p.add_argument('platform',choices=['macos','android','linux']);args=p.parse_args()
root=Path(__file__).resolve().parents[2]
if args.platform=='macos':raise SystemExit(subprocess.call([sys.executable,str(root/'scripts/unity/verify-macos-release.py')]))
output=root/'unity/TestResults'/('bluestacks/release' if args.platform=='android' else 'linux-release')/str(time.time_ns());output.mkdir(parents=True)
if args.platform=='linux':
    folder=root/'unity/TestResults/Linux'
    command=['docker','run','--rm','--platform','linux/amd64','--network','none','--shm-size=256m',
             '-v',str(folder)+':/app:ro','-v',str(output)+':/out','-v',str(root/'scripts/unity/docker/run-player.sh')+':/runner.sh:ro',
             'livliv-desktop-native:24.04','sh','/runner.sh','/app/LivLivVerification.x86_64','-batchmode','-force-glcore','-logFile','/out/player.log','-livlivReleaseProbe','/out']
    process=subprocess.run(command,timeout=180,capture_output=True,text=True);(output/'launcher.log').write_text(process.stdout+process.stderr)
    assert process.returncode==0,process.stderr
    hashes={str(f.relative_to(folder)):hashlib.sha256(f.read_bytes()).hexdigest() for f in folder.rglob('*') if f.is_file() and f.name in ['LivLivVerification.x86_64','UnityPlayer.so','GameAssembly.so','global-metadata.dat']}
else:
    adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb';serial='127.0.0.1:5555'
    def run(*items):return subprocess.check_output([adb,'-s',serial,*map(str,items)],text=True,stderr=subprocess.STDOUT,timeout=45)
    subprocess.run([adb,'connect',serial],check=True);assert run('get-state').strip()=='device'
    apk=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',apk)
    remote='/sdcard/Android/data/ai.livliv.verification/files/release-'+str(time.time_ns())
    command=['shell',f'am start -S -W -n ai.livliv.verification/com.unity3d.player.UnityPlayerGameActivity --es unity "-livlivReleaseProbe {remote}"']
    (output/'launch.log').write_text(run(*command));deadline=time.monotonic()+90
    while time.monotonic()<deadline:
        try:json.loads(run('shell','cat',remote+'/release.json'));break
        except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(.5)
    else:raise TimeoutError('Android release probe did not complete')
    run('pull',remote+'/.',output);hashes={'LivLivVerification.apk':hashlib.sha256(apk.read_bytes()).hexdigest()}
report=json.loads((output/'release.json').read_text());assert report['result']=='Passed',report
assert report['platform']==('Android' if args.platform=='android' else 'LinuxPlayer')
assert report['graphics']==('OpenGLES3' if args.platform=='android' else 'OpenGLCore')
for key in ['defaultRetained','immediateClaimCanceled','completedReleased','releasedRejected','recreatedRejected','otherRendered','mainAlive','targetObjectRetained','sharedOwnersNotified','retargetedOwnerPreserved']:assert report[key],key
assert report['otherActivePixels']>50 and report['mainEndFrame']>report['mainStartFrame']
files=['unity/Packages/ai.livliv.player/Runtime/release.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.Release.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Assets/Verification/PortVerification.Release.cs']
evidence={'result':'Passed','scope':'Logical target release; BlueStacks or Docker/Mesa, no physical mobile/GPU claim or hardware capture',
          'report':report,'command':command,'output':str(output),'bundleHashes':hashes,
          'sourceHashes':{f:hashlib.sha256((root/f).read_bytes()).hexdigest() for f in files}}
(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print(json.dumps({'result':'Passed','output':str(output),'report':report},indent=2))
