"""Audit built Windows/Linux players. Packaging evidence is not runtime evidence."""
from pathlib import Path
import argparse,hashlib,json,re,subprocess
p=argparse.ArgumentParser();p.add_argument('platform',choices=['windows','linux']);a=p.parse_args()
root=Path(__file__).resolve().parents[2];windows=a.platform=='windows'
folder=root/'unity/TestResults'/('Windows' if windows else 'Linux');data=folder/'LivLivVerification_Data'
binary=folder/('LivLivVerification.exe' if windows else 'LivLivVerification.x86_64')
tool=Path('/Applications/Unity/Hub/Editor/6000.3.14f1/PlaybackEngines/AndroidPlayer/NDK/toolchains/llvm/prebuilt/darwin-x86_64/bin')
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def run(*args):return subprocess.check_output(list(map(str,args)),text=True,stderr=subprocess.STDOUT)
def architecture(path):
    header=run(tool/'llvm-readobj','--file-headers',path)
    assert ('IMAGE_FILE_MACHINE_AMD64' if windows else 'EM_X86_64') in header,(path,header)
report={'platform':a.platform,'backend':'Mono' if windows else 'IL2CPP','binarySha256':sha(binary),'plugins':{},'resources':{},'foreignPlugins':[]}
architecture(binary)
plugins=[f for f in (data/'Plugins').rglob('*') if f.is_file()]
for f in plugins:
    with f.open('rb') as stream:magic=stream.read(4)
    expected=magic.startswith(b'MZ') if windows else magic==b'\x7fELF'
    if not expected:report['foreignPlugins'].append(str(f.relative_to(folder)))
    else:architecture(f)
webp='livlivwebp.dll' if windows else 'liblivlivwebp.so';media='mediapipe_c.dll' if windows else 'libmediapipe_c.so'
for name in [webp,media]:
    found=[f for f in plugins if f.name==name];assert len(found)==1,(name,found)
    f=found[0]
    if name==webp:
        source=root/'unity/Packages/ai.livliv.player/Plugins'/('Windows' if windows else 'Linux')/'x86_64'/name
    else:
        sources=list((root/'unity/Library/PackageCache').glob('com.github.homuler.mediapipe@*/Runtime/Plugins/'+name));assert len(sources)==1
        source=sources[0]
    assert sha(source)==sha(f),(name,'packaged bytes differ')
    if windows:
        dependencies=re.findall(r'  Name: (.+)',run(tool/'llvm-readobj','--coff-imports',f))
        exports=run(tool/'llvm-readobj','--coff-exports',f)
    else:
        dependencies=re.findall(r'Shared library: \[(.*?)\]',run(tool/'llvm-readelf','-d',f))
        exports=run(tool/'llvm-readelf','--dyn-syms','--wide',f)
    required=['WebPGetInfo','WebPDecodeRGBAInto'] if name==webp else ['mp_tasks_core_TaskRunner_Create__PKc_i_PF']
    for symbol in required:assert symbol in exports,(name,symbol)
    row={'path':str(f.relative_to(folder)),'sha256':sha(f),'bytes':f.stat().st_size,'dependencies':dependencies,'requiredExports':required}
    if not windows:
        row['versionRequirements']=sorted(set(re.findall(r'Name: ((?:GLIBCXX|GLIBC|CXXABI)_[\d.]+)',run(tool/'llvm-readelf','--version-info',f))))
    report['plugins'][name]=row
for name in ['unity/Assets/Verification/Resources/LivLivVerification.bytes','unity/Packages/ai.livliv.player/Browser~/face_landmarker.task','unity/Assets/Verification/Fonts/NanumGothic-Regular.ttf',
             'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-baseline.jpg',
             'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-progressive.jpg']:
    payload=(root/name).read_bytes()
    found=[str(f.relative_to(folder)) for f in data.glob('*.assets') if f.stat().st_size>=len(payload) and payload in f.read_bytes()]
    assert found,('Missing resource payload',name);report['resources'][name]=found
assembly=data/'Managed/Assembly-CSharp.dll' if windows else folder/'GameAssembly.so';report['assemblySha256']=sha(assembly)
if windows:
    report['managedLivLivHashes']={f.name:sha(f) for f in (data/'Managed').glob('LivLiv*.dll')}
    assert all(name in report['managedLivLivHashes'] for name in ['LivLiv.Runtime.dll','LivLiv.Unity.dll','LivLiv.UGUI.dll','LivLiv.MediaPipe.dll'])
names=['unity/Packages/ai.livliv.player/Runtime/Platform/JpegHeader.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/TextureDecoder.cs','unity/Packages/ai.livliv.ugui/Runtime/PosterPresenter.cs']
names+=['unity/Packages/ai.livliv.player/Runtime/base.cs','unity/Packages/ai.livliv.player/Runtime/loading.cs',
        'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityModelTransport.cs',
        'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLContextLoss.cs']
names+=['unity/Packages/ai.livliv.player/Runtime/Platform/ICaptureStatus.cs',
        'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/MicrophoneInput.cs',
        'unity/Packages/ai.livliv.mediapipe/Runtime/NativeFaceInput.cs',
        'unity/Packages/ai.livliv.mediapipe/Runtime/WebCamFaceSource.cs',
        'unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.Native.cs']
report['sourceHashes']={name:sha(root/name) for name in names}
if not windows:
    architecture(assembly)
    report['metadataSha256']=sha(data/'il2cpp_data/Metadata/global-metadata.dat')
files=[f for f in folder.rglob('*') if f.is_file()]
report['totalBytes']=sum(f.stat().st_size for f in files)
report['backupBytes']=sum(f.stat().st_size for f in files if any('BackUpThisFolder_ButDontShipItWithYourGame' in part for part in f.relative_to(folder).parts))
report['runtimeBundleBytes']=report['totalBytes']-report['backupBytes']
report['passed']=not report['foreignPlugins']
(root/'unity/TestResults'/f'{a.platform}-player-audit.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2));assert report['passed'],'Foreign platform binaries included'
