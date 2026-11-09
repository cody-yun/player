"""Read iOS test prerequisites, retaining only task-relevant fields (no device serials/accounts)."""
from pathlib import Path
import argparse,json,re,subprocess,tempfile

parser=argparse.ArgumentParser();parser.add_argument('--device',required=True);parser.add_argument('--output',type=Path,required=True);args=parser.parse_args()
def run(*command):return subprocess.run(command,capture_output=True,text=True)
with tempfile.TemporaryDirectory(prefix='livliv-device-') as temporary:
    path=Path(temporary)/'device.json'
    process=run('xcrun','devicectl','device','info','details','--device',args.device,'--timeout','20','--quiet','--json-output',str(path))
    data=json.loads(path.read_text()) if path.exists() else {}
device=data.get('result',{});properties=device.get('deviceProperties',{});hardware=device.get('hardwareProperties',{})
identities=run('security','find-identity','-v','-p','codesigning')
match=re.search(r'(\d+) valid identities found',identities.stdout)
count=int(match.group(1)) if match else None
report={'scope':'Environment prerequisites only; not signing/profile/install or runtime verification',
        'model':hardware.get('marketingName'),'osVersion':properties.get('osVersionNumber'),
        'developerMode':properties.get('developerModeStatus'),'ddiServicesAvailable':properties.get('ddiServicesAvailable',False),
        'deviceQueryExitCode':process.returncode,'validSigningIdentities':count,
        'xcode':run('xcodebuild','-version').stdout.strip(),'sdk':run('xcrun','--sdk','iphoneos','--show-sdk-version').stdout.strip()}
report['missing']=[]
if not device:report['missing'].append('Device information unavailable')
if report['developerMode']!='enabled':report['missing'].append('Developer Mode not enabled')
if not report['ddiServicesAvailable']:report['missing'].append('Developer disk image services unavailable')
if not count:report['missing'].append('No valid local code-signing identity')
report['environmentReady']=not report['missing']
args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
raise SystemExit(0 if report['environmentReady'] else 2)
