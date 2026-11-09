"""Run bounded 21-model lifecycle/timing probes; no camera, microphone or network input."""
from pathlib import Path
import argparse,hashlib,json,subprocess,time,shlex
from macos_player import resolve_app,launch,audit
parser=argparse.ArgumentParser();parser.add_argument('platform',choices=['macos','android']);args=parser.parse_args()
root=Path(__file__).resolve().parents[2];models=root/'unity/TestResults/model-soak-models'
assets=json.loads((models/'manifest.json').read_text());assert len(assets)==21
for name,item in assets.items():assert hashlib.sha256((models/(name+'.livliv')).read_bytes()).hexdigest()==item['sha256']
stamp=str(time.time_ns());output=root/'unity/TestResults'/('macos-model-soak' if args.platform=='macos' else 'bluestacks/model-soak')/stamp;output.mkdir(parents=True)
restore=None;bundle={}
try:
 if args.platform=='macos':
  app,binary,_=resolve_app('arm64');bundle=audit(app,'arm64')
  process=subprocess.Popen([*launch(binary,'arm64'),'-batchmode','-logFile',str(output/'player.log'),'-livlivModelSoak',str(output),'-livlivSoakModels',str(models)],stdout=(output/'launcher.log').open('w'),stderr=subprocess.STDOUT)
  def poll():
   p=output/'model-soak.json'
   return json.loads(p.read_text()) if p.exists() else None
 else:
  adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb';serial='127.0.0.1:5555';package='ai.livliv.verification';component=package+'/com.unity3d.player.UnityPlayerGameActivity'
  def run(*items):return subprocess.check_output([adb,'-s',serial,*map(str,items)],text=True,stderr=subprocess.STDOUT,timeout=40)
  subprocess.run([adb,'connect',serial],check=True);assert run('get-state').strip()=='device'
  apk=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',apk)
  remote='/sdcard/Android/data/'+package+'/files/model-soak-'+stamp
  run('shell','mkdir','-p',remote+'/models');run('push',str(models)+'/.',remote+'/models')
  restore=lambda:run('shell','am start -S -W -n '+component)
  options='-livlivModelSoak '+remote+' -livlivSoakModels '+remote+'/models'
  run('shell','am start -S -W -n '+component+' --es unity '+shlex.quote(options))
  bundle={'apkSha256':hashlib.sha256(apk.read_bytes()).hexdigest(),'apkBytes':apk.stat().st_size}
  def poll():
   try:return json.loads(run('shell','cat',remote+'/model-soak.json'))
   except subprocess.CalledProcessError:return None
 deadline=time.monotonic()+600;count=-1;report=None
 while time.monotonic()<deadline:
  try:report=poll()
  except json.JSONDecodeError:report=None
  if report:
   now=len(report.get('rows') or [])
   if now!=count:
    count=now;print(json.dumps({'platform':args.platform,'cycles':count,'result':report['result']}),flush=True)
   if report['result']!='Running':break
  if args.platform=='macos' and process.poll() is not None:raise RuntimeError('Player exited without a completed report')
  time.sleep(1)
 else:raise TimeoutError('Model soak did not finish')
 if args.platform=='macos':assert process.wait(timeout=30)==0,report.get('error')
 else:run('pull',remote+'/.',output)
 report.update(bundle);report['models']=assets;report['sourceHashes']={}
 for file in [root/'unity/Assets/Verification/PortVerification.ModelSoak.cs',*sorted((root/'unity/Packages/ai.livliv.player/Runtime').rglob('*.cs'))]:
  report['sourceHashes'][str(file.relative_to(root))]=hashlib.sha256(file.read_bytes()).hexdigest()
 (output/'evidence.json').write_text(json.dumps(report,indent=2)+'\n')
 assert report['result']=='Passed' and report['modelCount']==21 and len(report['rows'])==63,report.get('error')
 assert all(row['remainingObjects']==0 and row['frames']==30 for row in report['rows'])
 for name in assets:assert (output/(name+'.png')).stat().st_size>0,name
 print('PASSED '+str(output),flush=True)
finally:
 if args.platform=='macos' and 'process' in locals() and process.poll() is None:process.terminate();process.wait(timeout=15)
 if restore:restore()
