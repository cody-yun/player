"""Verify deferred target release in a built Metal player; no hardware capture or global context loss."""
from pathlib import Path
import hashlib,json,subprocess,time
from macos_player import resolve_app,launch,audit
root=Path(__file__).resolve().parents[2];app,binary,_=resolve_app('arm64');bundle=audit(app,'arm64')
output=root/'unity/TestResults/macos-release'/str(time.time_ns());output.mkdir(parents=True)
command=[*launch(binary,'arm64'),'-batchmode','-logFile',str(output/'player.log'),'-livlivReleaseProbe',str(output)]
process=subprocess.run(command,timeout=90,capture_output=True,text=True)
(output/'launcher.log').write_text(process.stdout+process.stderr)
report=json.loads((output/'release.json').read_text())
assert process.returncode==0 and report['result']=='Passed',report
assert report['platform']=='OSXPlayer' and report['graphics']=='Metal'
for key in ['defaultRetained','immediateClaimCanceled','completedReleased','releasedRejected','recreatedRejected','otherRendered','mainAlive','targetObjectRetained','sharedOwnersNotified','retargetedOwnerPreserved']:assert report[key],key
assert report['otherActivePixels']>50 and report['mainEndFrame']>report['mainStartFrame']
names=['unity/Packages/ai.livliv.player/Runtime/release.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.Release.cs',
       'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Assets/Verification/PortVerification.Release.cs']
evidence={'result':'Passed','scope':'Actual Metal logical-target release; independent player PNG alpha and main-player continuation; no device capture',
          'report':report,'command':command,'output':str(output),**bundle,
          'sourceHashes':{name:hashlib.sha256((root/name).read_bytes()).hexdigest() for name in names}}
(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'result':'Passed','output':str(output),'report':report},indent=2))
