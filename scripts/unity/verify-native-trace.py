"""Run generated trace recording/replay in built IL2CPP players; no camera/microphone."""
from pathlib import Path
import argparse,gzip,hashlib,json,plistlib,subprocess,time
p=argparse.ArgumentParser();p.add_argument('platform',choices=['macos','android']);args=p.parse_args()
root=Path(__file__).resolve().parents[2];stamp=str(time.time_ns());output=root/'unity/TestResults'/('macos-trace' if args.platform=='macos' else 'bluestacks/trace')/stamp;output.mkdir(parents=True)
restore=None
try:
 if args.platform=='macos':
  app=root/'unity/TestResults/MacOS/LivLivVerification.app';info=plistlib.loads((app/'Contents/Info.plist').read_bytes());artifact=app/'Contents/MacOS'/info['CFBundleExecutable']
  process=subprocess.run([str(artifact),'-batchmode','-logFile',str(output/'player.log'),'-livlivTraceSmoke',str(output)],timeout=60,capture_output=True,text=True);assert process.returncode==0,process.stderr
 else:
  adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb';serial='127.0.0.1:5555'
  def run(*items):return subprocess.check_output([adb,'-s',serial,*map(str,items)],text=True,stderr=subprocess.STDOUT,timeout=45)
  subprocess.run([adb,'connect',serial],check=True);artifact=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',artifact)
  component='ai.livliv.verification/com.unity3d.player.UnityPlayerGameActivity';remote='/sdcard/Android/data/ai.livliv.verification/files/trace-'+stamp
  restore=lambda:run('shell',f'am start -S -W -n {component} ')
  run('shell',f'am start -S -W -n {component} --es unity "-livlivTraceSmoke {remote}"')
  deadline=time.monotonic()+45
  while time.monotonic()<deadline:
   try:json.loads(run('shell','cat',remote+'/trace-player.json'));break
   except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(1)
  else:raise TimeoutError('Trace probe')
  run('pull',remote+'/.',output)
 report=json.loads((output/'trace-player.json').read_text());assert report['result']=='Passed',report
 assert report['platform']==('OSXPlayer' if args.platform=='macos' else 'Android'),report
 fixture=root/'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/traces.json.gz';expected=json.loads(gzip.decompress(fixture.read_bytes()))['pipeline'];assert report['states']==expected,(report['states'],expected)
 for narrow in [False,True]:
  frames=json.loads((output/('trace-narrow.json' if narrow else 'trace-normal.json')).read_text());assert len(frames)==1441 and frames[0]['t']==0 and frames[-1]['t']==48000
  assert all(a['t']<=b['t'] for a,b in zip(frames,frames[1:]));assert sum('m' not in f and 'b' not in f for f in frames)==120
 names=['src/runtime/trace.ts','src/runtime/tracking.ts','unity/Packages/ai.livliv.player/Runtime/runtime/trace.cs','unity/Packages/ai.livliv.player/Runtime/runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/Platform/TraceInput.cs','unity/Assets/Verification/PortVerification.Trace.cs']
 report.update(output=str(output),sourceHashes={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in names},artifactSha256=hashlib.sha256(artifact.read_bytes()).hexdigest())
 if args.platform=='macos':report['gameAssemblySha256']=hashlib.sha256((app/'Contents/Frameworks/GameAssembly.dylib').read_bytes()).hexdigest()
 (output/'evidence.json').write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report,indent=2))
finally:
 if restore:restore()
