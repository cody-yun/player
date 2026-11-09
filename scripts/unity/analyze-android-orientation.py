"""Compare recorded Unity/native orientation signals with the installed Android SDK constants.
Does not change a failed four-direction result or infer physical-device behavior.
"""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
parser=argparse.ArgumentParser();parser.add_argument('directory',type=Path);args=parser.parse_args()
directory=args.directory.resolve();source=directory/'orientation.json';report=json.loads(source.read_text())
editor=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer')
sdk=editor/'SDK/platforms/android-36/android.jar';javap=editor/'OpenJDK/bin/javap'
outputs={name:subprocess.check_output([str(javap),'-constants','-classpath',str(sdk),name],text=True) for name in ['android.content.pm.ActivityInfo','android.view.Surface']}
constants={name:{key:int(value) for key,value in re.findall(r'public static final int (\w+) = (-?\d+);',text)} for name,text in outputs.items()}
activity=constants['android.content.pm.ActivityInfo'];surface=constants['android.view.Surface']
names={'Portrait':('PORTRAIT','0'),'LandscapeLeft':('LANDSCAPE','90'),'PortraitUpsideDown':('REVERSE_PORTRAIT','180'),'LandscapeRight':('REVERSE_LANDSCAPE','270')}
assert len(report['phases'])==4 and set(p['requested'] for p in report['phases'])==set(names)
phases=[]
for phase in report['phases']:
    name=phase['requested'];native,rotation=names[name]
    assert not phase.get('nativeError'),phase.get('nativeError')
    requested=activity['SCREEN_ORIENTATION_'+native];expected=surface['ROTATION_'+rotation]
    phases.append({'requested':name,'expectedAndroidRequest':requested,'observedAndroidRequest':phase['androidRequested'],
                   'requestDelivered':phase['androidRequested']==requested,'expectedDisplayRotation':expected,
                   'observedDisplayRotation':phase['androidRotation'],'displayApplied':phase['androidRotation']==expected,
                   'unityObserved':phase['player']['layout']['orientation']})
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
analysis={'scope':'Native request delivery and display rotation on the recorded BlueStacks run; not a physical-device or sensor test',
          'fourOrientationResult':report['result'],'sourceSha256':sha(source),'sdk':str(sdk),'sdkSha256':sha(sdk),
          'sdkConstants':{k:{name:value for name,value in c.items() if name.startswith('SCREEN_ORIENTATION_') or name.startswith('ROTATION_')} for k,c in constants.items()},
          'requestDeliveryCount':sum(p['requestDelivered'] for p in phases),'displayAppliedCount':sum(p['displayApplied'] for p in phases),'phases':phases,
          'interpretation':'All four requests reached Android Activity; reverse requests did not produce the corresponding display rotations. Cause within Android/virtual-display handling remains unproven.' if all(p['requestDelivered'] for p in phases) and not all(p['displayApplied'] for p in phases) else 'Inspect individual request and display observations; no blanket platform diagnosis.'}
with (directory/'native-analysis.json').open('x') as stream:stream.write(json.dumps(analysis,indent=2)+'\n')
(directory/'sdk-constants.txt').write_text('\n'.join(outputs.values()))
print(json.dumps({'fourOrientationResult':report['result'],'requestDeliveryCount':analysis['requestDeliveryCount'],'displayAppliedCount':analysis['displayAppliedCount']},indent=2))
