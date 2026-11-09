"""Run the Android verification APK in BlueStacks via local ADB, using generated video.

The sample APK selects GLES3 by default; no graphics override is passed here.
No hardware camera or microphone is activated. Does not represent physical-device QA.
"""
from pathlib import Path
import argparse,hashlib,json,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('--require-expression-labels',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
adb=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb')
serial='127.0.0.1:5555'
apk=root/'unity/TestResults/Android/LivLivVerification.apk'
stamp=str(time.time_ns());output=root/'unity/TestResults/bluestacks/fixed-size-smoke'/stamp;output.mkdir(parents=True)
def run(*args):return subprocess.check_output([str(adb),'-s',serial,*map(str,args)],text=True,stderr=subprocess.STDOUT)
subprocess.run([str(adb),'connect',serial],check=True)
assert run('get-state').strip()=='device'
run('install','-r',apk)
# A unique directory prevents an earlier run's JSON from passing this run.
remote='/sdcard/Android/data/ai.livliv.verification/files/smoke-'+stamp
run('shell','am start -S -W -n ai.livliv.verification/com.unity3d.player.UnityPlayerGameActivity --es unity "-livlivSmoke '+remote+'"')
deadline=time.monotonic()+60
while time.monotonic()<deadline:
 try:
  evidence=json.loads(run('shell','cat',remote+'/native-player.json'));break
 except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(1)
else:raise TimeoutError('Android generated smoke did not write a result')
run('pull',remote+'/.',output)
assert evidence['result']=='Passed' and evidence['released'],evidence
assert evidence['platform']=='Android' and evidence['graphics']=='OpenGLES3'
states=evidence['states'];assert len(states)==10 and all(s['scale']==1 for s in states)
if args.require_expression_labels:
 assert evidence['expressionLabels'] and all(s['expressionCount']==4 and s['expressionLabel'] and s['expressionTranslation']=='Smile' for s in states)
assert evidence['sourceFailureHandled'] and evidence['restarted'] and evidence['failedSourceReleases']==evidence['restartedSourceReleases']==1
assert not states[7]['tracking'] and states[7]['faceStatus'].startswith('Failed:')
assert states[1]['faceSeen'] and abs(states[2]['angleZ'])>5 and not states[3]['faceSeen']
assert states[4]['faceSeen'] and states[5]['faceSeen']
assert all((output/(n+'.png')).stat().st_size>0 for n in ['neutral','detected','rotated','farther','nearer'])
files=['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.Native.cs','unity/Packages/ai.livliv.player/Runtime/tracking.cs',
       'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/compatibility.json']
evidence['sourceHashes']={f:hashlib.sha256((root/f).read_bytes()).hexdigest() for f in files}
evidence['apkSha256']=hashlib.sha256(apk.read_bytes()).hexdigest();evidence['apkBytes']=apk.stat().st_size
evidence['android']=run('shell','getprop','ro.build.version.release').strip()
evidence['abi']=run('shell','getprop','ro.product.cpu.abi').strip()
(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'result':evidence['result'],'output':str(output),'graphics':evidence['graphics'],'scales':[s['scale'] for s in states],'rotation':states[2]['angleZ'],'apkBytes':evidence['apkBytes'],'released':evidence['released']},indent=2))
