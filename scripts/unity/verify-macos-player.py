"""Run the built IL2CPP player with generated video; never requests camera/mic permission."""
from pathlib import Path
import argparse,hashlib,json,subprocess,time
from macos_player import resolve_app,launch,audit
parser=argparse.ArgumentParser();parser.add_argument('--architecture',choices=['arm64','x86_64'],default='arm64')
parser.add_argument('--require-expression-labels',action='store_true');args=parser.parse_args()
root=Path(__file__).resolve().parents[2];app,binary,plist=resolve_app(args.architecture);bundle=audit(app,args.architecture)
assert plist.get('NSCameraUsageDescription') and plist.get('NSMicrophoneUsageDescription')
output=root/'unity/TestResults'/('macos-intel-smoke' if args.architecture=='x86_64' else 'macos-smoke')/str(time.time_ns());output.mkdir(parents=True)
r=subprocess.run([*launch(binary,args.architecture),'-batchmode','-logFile',str(output/'player.log'),'-livlivSmoke',str(output)],timeout=90)
assert r.returncode==0,('Player failed',r.returncode)
evidence=json.loads((output/'native-player.json').read_text());assert evidence['result']=='Passed' and evidence['released']
assert evidence['platform']=='OSXPlayer' and evidence['graphics']=='Metal'
assert evidence['states'][1]['faceSeen'] and abs(evidence['states'][2]['angleZ'])>5 and not evidence['states'][3]['faceSeen']
assert len(evidence['states'])==10 and all(s['scale']==1 for s in evidence['states'])
if args.require_expression_labels:
    assert evidence['expressionLabels'] and all(s['expressionCount']==4 and s['expressionLabel'] and s['expressionTranslation']=='Smile' for s in evidence['states'])
assert evidence['sourceFailureHandled'] and evidence['restarted'] and evidence['failedSourceReleases']==evidence['restartedSourceReleases']==1
assert not evidence['states'][7]['tracking'] and evidence['states'][7]['faceStatus'].startswith('Failed:')
assert evidence['states'][5]['faceSeen']
assert all((output/(name+'.png')).stat().st_size>0 for name in ['neutral','detected','rotated','farther','nearer','restarted'])
files=['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.Native.cs','unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs','unity/Packages/ai.livliv.player/Plugins/macOS/liblivlivwebp.dylib']
files.append('unity/Packages/ai.livliv.player/Runtime/Platform/ICaptureStatus.cs')
files.extend(['unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/compatibility.json'])
evidence['sourceHashes']={f:hashlib.sha256((root/f).read_bytes()).hexdigest() for f in files}
evidence['executableSha256']=hashlib.sha256(binary.read_bytes()).hexdigest();evidence['plist']=plist;evidence.update(bundle);evidence['output']=str(output)
(output/'evidence.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'result':evidence['result'],'output':str(output),'platform':evidence['platform'],'graphics':evidence['graphics'],'expressionLabels':evidence.get('expressionLabels'),'rotation':evidence['states'][2]['angleZ'],'released':evidence['released']},indent=2))
