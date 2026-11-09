"""Bind consumer PlayMode evidence to exact tarball and PackageCache payload bytes."""
from pathlib import Path
import argparse,hashlib,json,tarfile,xml.etree.ElementTree as ET

parser=argparse.ArgumentParser();parser.add_argument('project',type=Path);parser.add_argument('--results',type=Path,required=True);args=parser.parse_args()
project=args.project.resolve();inputs=json.loads((project/'consumer-inputs.json').read_text())
manifest=json.loads((project/'Packages/manifest.json').read_text())['dependencies']
locked=json.loads((project/'Packages/packages-lock.json').read_text())['dependencies']
test=ET.parse(args.results).getroot();assert test.get('result')=='Passed' and test.get('passed')=='1' and test.get('skipped')=='0',test.attrib
evidence=json.loads((project/'Evidence/result.json').read_text());assert evidence['result']=='Passed' and evidence['disposed']
assert evidence['supported'], 'Installed Player.IsSupported must succeed before rendering'
assert evidence['contextLostHandled'], 'Installed Player must reject loads after confirmed context loss'
assert evidence['expressionLabels'], 'Installed Player.Expressions must retain concrete label/translation fields'
assert evidence['deferredRelease'], 'Installed Player must expose deferred release and reject spent targets'
assert evidence['pickedArchive'], 'Installed Player must accept a ZIP selected through the file-list overload'
assert evidence['graphics']=='Metal' and evidence['width']>0 and evidence['height']>0 and evidence['active']>1000
keys={'ai.livliv.player':'core','ai.livliv.ugui':'ui','ai.livliv.mediapipe':'native'}
packages={}
for item in inputs['packageManifest']['packages']:
    name=item['name']
    if name not in manifest:continue
    archive=Path(manifest[name].removeprefix('file:'));assert hashlib.sha256(archive.read_bytes()).hexdigest()==item['sha256']
    assert locked[name]['source']=='local-tarball'
    cached=Path(evidence[keys[name]]).resolve();assert cached.is_relative_to(project/'Library/PackageCache')
    for relative,expected in item['files'].items():
        if relative=='package.json':
            with tarfile.open(archive) as tar:original=json.loads(tar.extractfile('package/package.json').read())
            installed=json.loads((cached/relative).read_text());fingerprint=installed.pop('_fingerprint')
            assert original==installed and fingerprint.startswith(cached.name.split('@')[-1]),(name,'UPM metadata mismatch')
        else:assert hashlib.sha256((cached/relative).read_bytes()).hexdigest()==expected,(name,relative)
    assert not (cached/'Tests').exists()
    packages[name]={'sha256':item['sha256'],'filesVerified':len(item['files']),'resolvedPath':str(cached),
                    'packageJson':'Semantic match after removing only UPM-added _fingerprint','fingerprint':fingerprint}
native=inputs['native']['package']
if 'ai.livliv.mediapipe' in manifest:
    assert locked[native]['source']=='local-tarball'
    assert hashlib.sha256(Path(manifest[native].removeprefix('file:')).read_bytes()).hexdigest()==inputs['native']['sha256']
else:
    assert native not in locked and 'ai.livliv.mediapipe' not in locked
    assert not evidence.get('native')
report={'result':'Passed','project':str(project),'testResults':str(args.results.resolve()),'packages':packages,
        'nativeIncluded':native in locked,'render':evidence,'pngSha256':hashlib.sha256((project/'Evidence/consumer.png').read_bytes()).hexdigest(),
        'manifestSha256':hashlib.sha256((project/'Packages/manifest.json').read_bytes()).hexdigest(),
        'lockSha256':hashlib.sha256((project/'Packages/packages-lock.json').read_bytes()).hexdigest()}
(project/'Evidence/audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
