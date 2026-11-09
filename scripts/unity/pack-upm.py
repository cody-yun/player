"""Create deterministic local UPM tarballs. No publishing; no characters or repository tests."""
from pathlib import Path
import gzip,hashlib,io,json,shutil,tarfile

root=Path(__file__).resolve().parents[2]
output=root/'unity/TestResults/upm-distribution';output.mkdir(parents=True,exist_ok=True)
manifest={'schemaVersion':1,'packages':[],'excluded':['Tests/','Tests.meta','*.DS_Store'],
          'scope':'Local development distribution; platform/device completion remains unproven'}
for name in ['ai.livliv.player','ai.livliv.ugui','ai.livliv.mediapipe']:
    source=root/'unity/Packages'/name;package=json.loads((source/'package.json').read_text())
    archive=output/(name+'-'+package['version']+'.tgz');files={}
    temporary=archive.with_suffix('.tgz.tmp')
    with temporary.open('wb') as file,gzip.GzipFile(filename='',mode='wb',fileobj=file,mtime=0) as compressed,tarfile.open(fileobj=compressed,mode='w',format=tarfile.PAX_FORMAT) as tar:
        for path in sorted(source.rglob('*')):
            relative=path.relative_to(source)
            if relative.parts[0] in ['Tests','Tests.meta'] or path.name=='.DS_Store':continue
            if path.is_symlink():raise ValueError('Package symlink: '+str(path))
            if not path.is_file():continue
            data=path.read_bytes();entry=tarfile.TarInfo('package/'+relative.as_posix())
            entry.size=len(data);entry.mode=0o644;entry.mtime=0;tar.addfile(entry,io.BytesIO(data))
            files[relative.as_posix()]=hashlib.sha256(data).hexdigest()
    assert all(key in files for key in ['package.json','LICENSE.md','NOTICE.md'])
    with tarfile.open(temporary) as tar:
        assert len(tar.getmembers())==len(files)
        for entry in tar:
            assert entry.isfile() and entry.name.startswith('package/')
            assert hashlib.sha256(tar.extractfile(entry).read()).hexdigest()==files[entry.name[len('package/'):]]
    temporary.replace(archive)
    manifest['packages'].append({'name':name,'version':package['version'],'archive':archive.name,
                                'bytes':archive.stat().st_size,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'files':files})
native=json.loads((root/'unity/ThirdParty/mediapipe-native.json').read_text())
native_source=root/'unity/ThirdParty'/native['archive'];native_target=output/native['archive']
assert hashlib.sha256(native_source.read_bytes()).hexdigest()==native['sha256']
if not native_target.exists() or hashlib.sha256(native_target.read_bytes()).hexdigest()!=native['sha256']:
    shutil.copyfile(native_source,native_target)
manifest['externalDependencies']=[native]
(output/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
instructions=root/'docs/unity/INSTALL.md'
if instructions.exists():shutil.copyfile(instructions,output/'INSTALL.md')
print(json.dumps([{k:v for k,v in p.items() if k!='files'} for p in manifest['packages']],indent=2))
