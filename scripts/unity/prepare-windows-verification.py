"""Bundle an audited player and portable runner for an actual Windows machine. No publishing."""
from pathlib import Path
import argparse,importlib.util,json,zipfile
root=Path(__file__).resolve().parents[2]
source=root/'unity/TestResults/Windows'
parser=argparse.ArgumentParser();parser.add_argument('--output-directory',type=Path,default=root/'unity/TestResults/windows-verification')
args=parser.parse_args();output=args.output_directory.resolve();output.mkdir(parents=True,exist_ok=True)
archive=output/'LivLivWindowsVerification.zip'
spec=importlib.util.spec_from_file_location('windows_runner',root/'scripts/unity/verify-windows-player.py')
runner=importlib.util.module_from_spec(spec);spec.loader.exec_module(runner)
audit=json.loads((root/'unity/TestResults/windows-player-audit.json').read_text())
assert audit['passed'] and audit['platform']=='windows'
assert runner.sha(source/'LivLivVerification.exe')==audit['binarySha256']
assert runner.sha(source/'LivLivVerification_Data/Managed/Assembly-CSharp.dll')==audit['assemblySha256']
for name,digest in audit['managedLivLivHashes'].items():
    assert runner.sha(source/'LivLivVerification_Data/Managed'/name)==digest
for name,digest in audit['sourceHashes'].items():assert runner.sha(root/name)==digest,('Stale player audit',name)
baseline=json.loads((root/'unity/Packages/ai.livliv.player/compatibility.json').read_text())['commit']
files={}
for path in sorted(source.rglob('*')):
    if path.is_symlink():raise ValueError('Runtime symlink: '+str(path))
    if path.is_file() and not any('BackUpThisFolder_ButDontShipItWithYourGame' in p for p in path.parts):
        files['Player/'+path.relative_to(source).as_posix()]=path
files['verify-windows-player.py']=root/'scripts/unity/verify-windows-player.py'
files['README.md']=root/'scripts/unity/windows-verification.md'
files['Proof/windows-player-audit.json']=root/'unity/TestResults/windows-player-audit.json'
for package in ['ai.livliv.player','ai.livliv.ugui','ai.livliv.mediapipe']:
    for name in ['LICENSE.md','NOTICE.md']:
        files['Licenses/'+package+'/'+name]=root/'unity/Packages'/package/name
for path in sorted((root/'unity/Packages/ai.livliv.player/ThirdParty').glob('*')):
    if path.is_file() and path.suffix!='.meta':files['Licenses/ai.livliv.player/ThirdParty/'+path.name]=path
for name in ['LICENSE.md','CREDITS.md']:files['Licenses/characters/'+name]=root/'characters'/name
native=list((root/'unity/Library/PackageCache').glob('com.github.homuler.mediapipe@*'));assert len(native)==1
for name in ['LICENSE.md','Third Party Notices.md']:
    path=native[0]/name
    if not path.exists():raise FileNotFoundError(path)
    files['Licenses/homuler/'+name]=path
json_package=list((root/'unity/Library/PackageCache').glob('com.unity.nuget.newtonsoft-json@*'));assert len(json_package)==1
for name in ['LICENSE.md','Third Party Notices.md']:
    files['Licenses/Newtonsoft.Json/'+name]=json_package[0]/name
manifest={'schemaVersion':1,'platform':'windows-x64','baseline':baseline,
          'executable':'Player/LivLivVerification.exe','scope':'Prepared transfer bundle; Windows runtime is not verified by packaging',
          'files':{name:runner.sha(path) for name,path in sorted(files.items())}}
temporary=archive.with_suffix('.zip.tmp')
with zipfile.ZipFile(temporary,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as zipped:
    for name,path in sorted(files.items()):
        entry=zipfile.ZipInfo(name);entry.compress_type=zipfile.ZIP_DEFLATED;entry.external_attr=0o100644<<16
        zipped.writestr(entry,path.read_bytes())
    entry=zipfile.ZipInfo('bundle-manifest.json');entry.compress_type=zipfile.ZIP_DEFLATED;entry.external_attr=0o100644<<16
    zipped.writestr(entry,json.dumps(manifest,indent=2)+'\n')
temporary.replace(archive)
(output/'bundle-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
report={'result':'Prepared','runtimeExecuted':False,'archive':str(archive),'bytes':archive.stat().st_size,
        'sha256':runner.sha(archive),'files':len(files),'baseline':baseline}
(output/'package.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
