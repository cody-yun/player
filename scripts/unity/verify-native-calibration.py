"""Verify calibration across distinct player launches; only isolated QA keys are written/deleted."""
from pathlib import Path
import argparse,hashlib,json,plistlib,subprocess,time,uuid
parser=argparse.ArgumentParser();parser.add_argument('platform',choices=['macos','android']);args=parser.parse_args()
root=Path(__file__).resolve().parents[2];stamp=str(time.time_ns());identity='qa-calibration-'+uuid.uuid4().hex
output=root/'unity/TestResults'/('macos-calibration' if args.platform=='macos' else 'bluestacks/calibration')/stamp;output.mkdir(parents=True)
phases=['write','restore','legacy','restore','malformed'];reports=[]
if args.platform=='macos':
 app=root/'unity/TestResults/MacOS/LivLivVerification.app';info=plistlib.loads((app/'Contents/Info.plist').read_bytes());artifact=app/'Contents/MacOS'/info['CFBundleExecutable']
 def launch(phase,index):
  directory=output/str(index);directory.mkdir(exist_ok=True)
  p=subprocess.run([str(artifact),'-batchmode','-logFile',str(directory/'player.log'),'-livlivCalibrationSmoke',str(directory),phase,identity],timeout=60,capture_output=True,text=True)
  (directory/'launcher.log').write_text(p.stdout+p.stderr);assert p.returncode==0,(phase,p.returncode)
  return json.loads((directory/(phase+'.json')).read_text())
else:
 adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb';serial='127.0.0.1:5555'
 def run(*items):return subprocess.check_output([adb,'-s',serial,*map(str,items)],text=True,stderr=subprocess.STDOUT,timeout=45)
 subprocess.run([adb,'connect',serial],check=True);assert run('get-state').strip()=='device'
 artifact=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',artifact)
 component='ai.livliv.verification/com.unity3d.player.UnityPlayerGameActivity'
 def launch(phase,index):
  directory=output/str(index);directory.mkdir(exist_ok=True);remote='/sdcard/Android/data/ai.livliv.verification/files/calibration-'+stamp+'/'+str(index)
  run('shell',f'am start -S -W -n {component} --es unity "-livlivCalibrationSmoke {remote} {phase} {identity}"')
  deadline=time.monotonic()+45
  while time.monotonic()<deadline:
   try:
    result=json.loads(run('shell','cat',remote+'/'+phase+'.json'));run('pull',remote+'/.',directory);return result
   except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(1)
  raise TimeoutError(phase)
try:
 for index,phase in enumerate(phases):
  report=launch(phase,index);assert report['result']=='Passed',report;assert report['id']==identity,report;reports.append(report)
 # The read-only restart keeps the exact serialized bytes, including the original timestamp.
 assert reports[0]['stored']==reports[1]['stored']
 assert reports[2]['stored']==reports[3]['stored']
 sources=['src/runtime/tracking.ts','src/tracking.ts','unity/Packages/ai.livliv.player/Runtime/runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/Platform/ICalibrationStore.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityCalibrationStore.cs','unity/Assets/Verification/PortVerification.Calibration.cs','unity/Assets/Verification/PortVerification.cs']
 evidence={'result':'Passed','platform':args.platform,'output':str(output),'states':reports,'sourceHashes':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in sources},'artifactSha256':hashlib.sha256(artifact.read_bytes()).hexdigest()}
 if args.platform=='macos':evidence['gameAssemblySha256']=hashlib.sha256((app/'Contents/Frameworks/GameAssembly.dylib').read_bytes()).hexdigest()
finally:
 try:assert launch('cleanup',len(phases))['removed']
 finally:
  if args.platform=='android':run('shell',f'am start -S -W -n {component} ')
evidence['cleanup']=True;(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n');print(json.dumps(evidence,indent=2))
