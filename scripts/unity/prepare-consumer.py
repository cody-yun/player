"""Make a new isolated project, referencing packed tarballs rather than source packages."""
from pathlib import Path
import argparse,hashlib,json,shutil,time,zipfile

parser=argparse.ArgumentParser();parser.add_argument('--without-native',action='store_true');args=parser.parse_args()

root=Path(__file__).resolve().parents[2];distribution=root/'unity/TestResults/upm-distribution'
manifest=json.loads((distribution/'manifest.json').read_text())
project=root/'unity/TestResults/consumers'/str(time.time_ns());project.mkdir(parents=True)
for name in ['Assets/ConsumerTests','Packages','ProjectSettings']:(project/name).mkdir(parents=True)
dependencies={}
for package in manifest['packages']:
    if args.without_native and package['name']=='ai.livliv.mediapipe':continue
    archive=distribution/package['archive'];assert hashlib.sha256(archive.read_bytes()).hexdigest()==package['sha256']
    dependencies[package['name']]='file:'+str(archive)
native=manifest['externalDependencies'][0];archive=distribution/native['archive']
assert hashlib.sha256(archive.read_bytes()).hexdigest()==native['sha256']
if not args.without_native:dependencies[native['package']]='file:'+str(archive)
dependencies['com.unity.modules.jsonserialize']='1.0.0' # Used only by the consumer evidence writer.
dependencies['com.unity.test-framework']='1.6.0' # Consumer verification harness, not the runtime package.
(project/'Packages/manifest.json').write_text(json.dumps({'dependencies':dependencies},indent=2)+'\n')
shutil.copyfile(root/'unity/ProjectSettings/ProjectVersion.txt',project/'ProjectSettings/ProjectVersion.txt')
smoke=(root/'scripts/unity/consumer/ConsumerSmoke.cs').read_text()
if not args.without_native:smoke='#define LIVLIV_CONSUMER_NATIVE\n'+smoke
(project/'Assets/ConsumerTests/ConsumerSmoke.cs').write_text(smoke)
references=['LivLiv.Runtime','LivLiv.Unity','LivLiv.UGUI','Unity.ugui']
if not args.without_native:references.append('LivLiv.MediaPipe')
(project/'Assets/ConsumerTests/LivLiv.Consumer.Tests.asmdef').write_text(json.dumps({
    'name':'LivLiv.Consumer.Tests','references':references,'overrideReferences':True,
    'precompiledReferences':['nunit.framework.dll','Newtonsoft.Json.dll'],'optionalUnityReferences':['TestAssemblies']},indent=2)+'\n')
with zipfile.ZipFile(project/'Assets/ConsumerModel.bytes','w',zipfile.ZIP_DEFLATED) as archive:
    model=root/'characters/char06'
    for path in sorted(model.rglob('*')):
        if path.is_file():archive.write(path,path.relative_to(model).as_posix())
# Only this disposable test project receives a repository model; it is not in the distribution.
(project/'consumer-inputs.json').write_text(json.dumps({'packageManifest':manifest,'native':native,
    'modelSha256':hashlib.sha256((project/'Assets/ConsumerModel.bytes').read_bytes()).hexdigest()},indent=2)+'\n')
print(project)
