"""Shared read-only architecture/provenance checks for native Mac verification players."""
from pathlib import Path
import hashlib,platform,plistlib,struct,subprocess,tempfile

ROOT=Path(__file__).resolve().parents[2]
def resolve_app(architecture):
    app=ROOT/'unity/TestResults'/('MacOSIntel' if architecture=='x86_64' else 'MacOS')/'LivLivVerification.app'
    plist=plistlib.loads((app/'Contents/Info.plist').read_bytes())
    return app,app/'Contents/MacOS'/plist['CFBundleExecutable'],plist

def launch(binary,architecture):return ['/usr/bin/arch','-'+architecture,str(binary)]

def audit(app,architecture):
    def run(*args):return subprocess.check_output(list(map(str,args)),text=True,stderr=subprocess.STDOUT).strip()
    def sha(file):return hashlib.sha256(file.read_bytes()).hexdigest()
    plist=plistlib.loads((app/'Contents/Info.plist').read_bytes())
    assert tuple(map(int,plist['LSMinimumSystemVersion'].split('.'))) >= (13,0), 'Bundled MediaPipe requires macOS 13.0'
    assert plist.get('NSCameraUsageDescription') and plist.get('NSMicrophoneUsageDescription')
    names=['Contents/MacOS/'+plist['CFBundleExecutable'],'Contents/Frameworks/GameAssembly.dylib',
           'Contents/Frameworks/UnityPlayer.dylib','Contents/PlugIns/liblivlivwebp.dylib','Contents/PlugIns/libmediapipe_c.dylib']
    architectures={n:run('lipo','-archs',app/n).split() for n in names}
    assert architectures['Contents/Frameworks/GameAssembly.dylib']==[architecture],architectures
    assert all(architecture in value for value in architectures.values()),architectures
    sources={'Contents/PlugIns/liblivlivwebp.dylib':ROOT/'unity/Packages/ai.livliv.player/Plugins/macOS/liblivlivwebp.dylib'}
    media=list((ROOT/'unity/Library/PackageCache').glob('com.github.homuler.mediapipe@*/Runtime/Plugins/libmediapipe_c.dylib'));assert len(media)==1
    sources['Contents/PlugIns/libmediapipe_c.dylib']=media[0]
    provenance={}
    for name,source in sources.items():
        record={'sourceSha256':sha(source),'packagedSha256':sha(app/name)}
        if record['sourceSha256']!=record['packagedSha256']:
            # Unity signs the previously unsigned MediaPipe x64 slice. Strip only
            # signatures in temporary copies, then normalize __LINKEDIT vmsize:
            # codesign leaves its enlarged allocation after signature removal.
            # Every other byte (including all code/data/load commands) must match.
            assert name.endswith('/libmediapipe_c.dylib'),(name,'packaged bytes differ')
            hashes={}
            with tempfile.TemporaryDirectory(prefix='livliv-macho-') as directory:
                for arch in architectures[name]:
                    pair=[]
                    for label,path in [('source',source),('packaged',app/name)]:
                        thin=Path(directory)/(label+'-'+arch+'.dylib')
                        run('lipo',path,'-thin',arch,'-output',thin)
                        run('codesign','--remove-signature',thin)
                        data=bytearray(thin.read_bytes())
                        assert struct.unpack_from('<I',data)[0]==0xfeedfacf
                        count=struct.unpack_from('<I',data,16)[0];offset=32;found=False
                        for _ in range(count):
                            command,size=struct.unpack_from('<II',data,offset)
                            assert size>=8 and offset+size<=len(data)
                            assert command!=0x1d,'Signature load command remains'
                            if command==0x19 and data[offset+8:offset+24].rstrip(b'\0')==b'__LINKEDIT':
                                vmsize,fileoff,filesize=struct.unpack_from('<QQQ',data,offset+32)
                                assert vmsize>=filesize and fileoff+filesize==len(data)
                                struct.pack_into('<Q',data,offset+32,0);found=True
                            offset+=size
                        assert found
                        pair.append(hashlib.sha256(data).hexdigest())
                    assert pair[0]==pair[1],(name,arch,'non-signature bytes differ')
                    hashes[arch]=pair[0]
            record['signatureNormalizedSliceHashes']=hashes
        provenance[name]=record
    for name,symbols in [('liblivlivwebp.dylib',['_WebPGetInfo','_WebPDecodeRGBAInto']),('libmediapipe_c.dylib',['_mp_tasks_core_TaskRunner_Create__PKc_i_PF'])]:
        symbols_text=run('nm','-arch',architecture,'-gU',app/'Contents/PlugIns'/name)
        for symbol in symbols:assert symbol in symbols_text,(name,symbol)
    for name in sources:run('codesign','--verify','--strict',app/name)
    names += ['Contents/Resources/Data/il2cpp_data/Metadata/global-metadata.dat','Contents/Resources/Data/resources.assets','Contents/Resources/Data/sharedassets0.assets']
    return {'requestedArchitecture':architecture,'hostArchitecture':platform.machine(),'minimumMacOS':plist['LSMinimumSystemVersion'],
            'architectures':architectures,'bundleHashes':{n:sha(app/n) for n in names},'pluginProvenance':provenance,
            'runtimeScope':'Forced '+architecture+' launch on '+platform.machine()+' host; x86_64 on ARM64 uses Rosetta, not physical Intel hardware'}
