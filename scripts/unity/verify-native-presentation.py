"""Run built native presentation probes: no camera/mic/network. Android uses authorized BlueStacks."""
from pathlib import Path
import argparse,hashlib,json,plistlib,subprocess,time
from macos_player import resolve_app,launch,audit
parser=argparse.ArgumentParser();parser.add_argument('platform',choices=['macos','android','linux']);parser.add_argument('--architecture',choices=['arm64','x86_64'],default='arm64');args=parser.parse_args()
if args.platform!='macos' and args.architecture!='arm64':parser.error('--architecture is only for macos')
root=Path(__file__).resolve().parents[2]
output=root/'unity/TestResults'/({'macos':'macos-presentation','android':'bluestacks/presentation','linux':'linux-presentation'}[args.platform])/str(time.time_ns())
if args.platform=='macos' and args.architecture=='x86_64':output=root/'unity/TestResults/macos-intel-presentation'/str(time.time_ns())
output.mkdir(parents=True)
files=[
 'unity/Packages/ai.livliv.player/Runtime/Platform/JpegHeader.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/TextureDecoder.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityModelTransport.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLContextLoss.cs',
 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-baseline.jpg',
 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-progressive.jpg',
 'unity/Packages/ai.livliv.player/Runtime/runtime/puppet.cs',
 'unity/Assets/Verification/PortVerification.CameraSurfaces.cs',
 'unity/Assets/Verification/PortVerification.Presentation.cs',
 'unity/Assets/Verification/PortVerification.TextMark.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/IMarkTextRasterizer.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/BrandRasterizer.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityMark.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityMarkText.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivTextCoverage.shader',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivMarkFont.ttf',
 'unity/Assets/Verification/Fonts/NanumGothic-Regular.ttf',
 'unity/Assets/Verification/PortVerification.MasksNative.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/MaskBounds.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/PuppetRenderer.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/PuppetRenderer.Draw.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivLayer.shader',
 'unity/Assets/Verification/PortVerification.cs',
 'unity/Assets/Verification/VerificationHud.cs',
 'unity/Packages/ai.livliv.player/Runtime/base.cs',
 'unity/Packages/ai.livliv.player/Runtime/loading.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/SurfaceSize.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs',
 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.Snapshot.cs',
 'unity/Packages/ai.livliv.ugui/Runtime/PlayerSurface.cs',
 'unity/Packages/ai.livliv.ugui/Runtime/PosterPresenter.cs',
 'unity/Packages/ai.livliv.ugui/Runtime/Resources/LivLivSurface.shader',
]
restore=None
try:
 if args.platform=='macos':
  app,artifact,plist=resolve_app(args.architecture);bundle=audit(app,args.architecture)
  process=subprocess.run([*launch(artifact,args.architecture),'-batchmode','-logFile',str(output/'player.log'),'-livlivPresentationSmoke',str(output)],timeout=90,capture_output=True,text=True)
  (output/'launcher.log').write_text(process.stdout+process.stderr)
 elif args.platform=='linux':
  folder=root/'unity/TestResults/Linux';artifact=folder/'LivLivVerification.x86_64'
  command=['docker','run','--rm','--platform','linux/amd64','--network','none','--shm-size=256m',
           '-v',str(folder)+':/app:ro','-v',str(output)+':/out',
           '-v',str(root/'scripts/unity/docker/run-player.sh')+':/runner.sh:ro','livliv-desktop-native:24.04',
           'sh','/runner.sh','/app/LivLivVerification.x86_64',
           '-batchmode','-force-glcore','-logFile','/out/player.log','-livlivPresentationSmoke','/out']
  process=subprocess.run(command,timeout=180,capture_output=True,text=True)
  (output/'launcher.log').write_text(process.stdout+process.stderr)
 else:
  adb='/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb';serial='127.0.0.1:5555'
  def run(*items):return subprocess.check_output([adb,'-s',serial,*map(str,items)],text=True,stderr=subprocess.STDOUT)
  subprocess.run([adb,'connect',serial],check=True);assert run('get-state').strip()=='device'
  artifact=root/'unity/TestResults/Android/LivLivVerification.apk';run('install','-r',artifact)
  component='ai.livliv.verification/com.unity3d.player.UnityPlayerGameActivity'
  remote='/sdcard/Android/data/ai.livliv.verification/files/presentation-'+str(time.time_ns())
  restore=lambda:run('shell',f'am start -S -W -n {component} ')
  run('shell',f'am start -S -W -n {component} --es unity "-livlivPresentationSmoke {remote}"')
  deadline=time.monotonic()+90
  while time.monotonic()<deadline:
   try:
    json.loads(run('shell','cat',remote+'/presentation-player.json'));break
   except (subprocess.CalledProcessError,json.JSONDecodeError):time.sleep(1)
  else:raise TimeoutError('Android presentation probe did not produce evidence')
  run('pull',remote+'/.',output)
 report=json.loads((output/'presentation-player.json').read_text())
 report['sourceHashes']={p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in files}
 report['artifactSha256']=hashlib.sha256(artifact.read_bytes()).hexdigest();report['artifactBytes']=artifact.stat().st_size;report['output']=str(output)
 if args.platform=='macos':
  report.update(bundle)
 if args.platform=='linux':
  # Plugin architecture subdirectories are Unity-version dependent; record actual paths.
  report['bundleHashes']={str(f.relative_to(folder)):hashlib.sha256(f.read_bytes()).hexdigest() for f in folder.rglob('*') if f.is_file() and (f.name in ['UnityPlayer.so','GameAssembly.so','global-metadata.dat','libmediapipe_c.so','liblivlivwebp.so'])}
  report['runtimeEnvironment']='Docker linux/amd64 on macOS ARM64, Xvfb + Mesa software OpenGL; no physical Linux GPU or devices'
  report['containerImage']=subprocess.check_output(['docker','image','inspect','livliv-desktop-native:24.04','--format','{{.Id}}'],text=True).strip()
 report['posterLifecycle']='controlled PlayerBase host; real UGUI PNG/WebP/JPEG data/file decode; no HTTP'
 if args.platform=='android':report['android']=run('shell','getprop','ro.build.version.release').strip();report['abi']=run('shell','getprop','ro.product.cpu.abi').strip()
 assert report['result']=='Passed',report
 assert report['platform']=={'macos':'OSXPlayer','android':'Android','linux':'LinuxPlayer'}[args.platform],report
 assert report['graphics']=={'macos':'Metal','android':'OpenGLES3','linux':'OpenGLCore'}[args.platform],report
 assert all(report[k] for k in ['pngPoster','webpFilePoster','invalidPoster','latePoster','disposed']),report
 assert report['supported'] and report['contextLostHandled'] and report['supportProbes']==0,report
 assert report['jpegPosters']==['baseline','progressive'],report
 for kind in report['jpegPosters']:assert (output/('jpeg-'+kind+'.png')).stat().st_size>0
 assert all(report[k]==0 for k in ['targets','materials','posterTextures','maskTextures']),report
 states=report['states'];assert [s['name'] for s in states]==['portrait','reuse','landscape','density','budget','zero','recovered']
 assert states[0]['targetId']==states[1]['targetId'] and states[2]['targetId']!=states[1]['targetId']
 for s in states:
  assert s['time']==0 and s['scale']==1,s
  if s['name']=='zero':assert s['width']==s['height']==0
  else:assert s['active']>100 and s['roundtrip']<=1,s
 assert states[4]['limited'] and states[4]['width']*states[4]['height']<=10000
 cameras=report['cameraSurfaces'];assert [s['name'] for s in cameras]==['front','right','left','up','down','orthographic','screen','nested']
 for row in cameras:
  assert abs(row['width']-row['expectedWidth'])<=1 and abs(row['height']-row['expectedHeight'])<=1,row
  assert row['active']>2000 and row['time']==0 and row['scale']==1,row
  assert (output/('camera-'+row['name']+'.png')).stat().st_size>0,row
 masks=report['masks'];assert [s['name'] for s in masks]==['glance','halfblink','combined','reused','larger','smaller','offscreen','recovered']
 for row in masks:
  assert row['scale']==1 and row['storageBytes']<row['fullSizeBytes'],row
  assert row['channels']==(0 if row['name']=='offscreen' else 2),row
 assert masks[2]['maskId']==masks[3]['maskId'] and masks[4]['maskId']!=masks[3]['maskId']
 assert masks[4]['maskId']==masks[5]['maskId']==masks[6]['maskId'] and masks[7]['maskId']!=masks[6]['maskId']
 text_marks=report['textMarks'];assert [s['name'] for s in text_marks]==['empty','latin','korean','literal','default','retina-empty','retina','budget']
 for row in text_marks:
  assert all(row[k]==0 for k in ['watermarks','coverageTargets','coverageMeshes','coverageMaterials']),row
  assert row['font']==('LivLivMarkFont' if row['name']=='default' else 'NanumGothic-Regular'),row
  if row['name']=='budget':assert row['code']=='too-large',row
  else:assert not row.get('error') and (output/('text-'+row['name']+'.png')).exists(),row
 if args.platform in ['macos','linux']:assert process.returncode==0,process.returncode
 (output/'evidence.json').write_text(json.dumps(report,indent=2)+'\n')
 print(json.dumps(report,indent=2))
finally:
 if restore:restore()
