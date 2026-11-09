"""Execute generated MediaPipe/video + renderer probe in the built Linux player.

Docker x86_64 emulation + Xvfb/Mesa; no physical camera, microphone, GPU, or network.
"""
from pathlib import Path
import argparse,hashlib,json,subprocess,time
parser=argparse.ArgumentParser();parser.add_argument('--require-expression-labels',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[2];folder=root/'unity/TestResults/Linux'
output=root/'unity/TestResults/linux-face'/str(time.time_ns());output.mkdir(parents=True)
command=['docker','run','--rm','--platform','linux/amd64','--network','none','--shm-size=256m',
         '-v',str(folder)+':/app:ro','-v',str(output)+':/out',
         '-v',str(root/'scripts/unity/docker/run-player.sh')+':/runner.sh:ro','livliv-desktop-native:24.04',
         'sh','/runner.sh','/app/LivLivVerification.x86_64',
         '-batchmode','-force-glcore','-logFile','/out/player.log','-livlivSmoke','/out']
process=subprocess.run(command,timeout=180,capture_output=True,text=True)
(output/'launcher.log').write_text(process.stdout+process.stderr)
report=json.loads((output/'native-player.json').read_text())
assert report['result']=='Passed' and report['released'],report
assert report['platform']=='LinuxPlayer' and report['graphics']=='OpenGLCore',report
states=report['states'];assert len(states)==10 and all(s['scale']==1 for s in states)
if args.require_expression_labels:
    assert report['expressionLabels'] and all(s['expressionCount']==4 and s['expressionLabel'] and s['expressionTranslation']=='Smile' for s in states)
assert report['sourceFailureHandled'] and report['restarted'] and report['failedSourceReleases']==report['restartedSourceReleases']==1
assert not states[7]['tracking'] and states[7]['faceStatus'].startswith('Failed:')
assert states[1]['faceSeen'] and abs(states[2]['angleZ'])>5 and not states[3]['faceSeen']
assert states[4]['faceSeen'] and states[5]['faceSeen'] and process.returncode==0
for name in ['neutral','detected','rotated','farther','nearer','restarted']:assert (output/(name+'.png')).stat().st_size>0
report['output']=str(output);report['runtimeEnvironment']='Docker linux/amd64 on macOS ARM64, Xvfb/Mesa software OpenGL; generated portrait video'
report['containerImage']=subprocess.check_output(['docker','image','inspect','livliv-desktop-native:24.04','--format','{{.Id}}'],text=True).strip()
report['bundleHashes']={str(f.relative_to(folder)):hashlib.sha256(f.read_bytes()).hexdigest() for f in folder.rglob('*') if f.is_file() and f.name in ['LivLivVerification.x86_64','UnityPlayer.so','GameAssembly.so','global-metadata.dat','libmediapipe_c.so','liblivlivwebp.so']}
names=['unity/Assets/Verification/PortVerification.Native.cs','unity/Assets/Verification/PortVerification.cs','unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs','unity/Packages/ai.livliv.player/Runtime/tracking.cs']
names.append('unity/Packages/ai.livliv.player/Runtime/runtime/puppet.cs')
names.append('unity/Packages/ai.livliv.player/Runtime/Platform/ICaptureStatus.cs')
names.extend(['unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/compatibility.json'])
report['sourceHashes']={n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in names}
(output/'evidence.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps({'result':report['result'],'output':str(output),'rotation':states[2]['angleZ'],'released':report['released']},indent=2))
