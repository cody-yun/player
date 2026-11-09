"""Inspect the actual APK; successful packaging is not a device/runtime test."""
from pathlib import Path
import hashlib, json, os, re, subprocess, sys, zipfile
root=Path(__file__).resolve().parents[2]
editor=Path(sys.argv[1] if len(sys.argv)>1 else '/Applications/Unity/Hub/Editor/6000.3.14f1')
android=editor/'PlaybackEngines/AndroidPlayer';buildtools=android/'SDK/build-tools/36.0.0'
readelf=android/'NDK/toolchains/llvm/prebuilt/darwin-x86_64/bin/llvm-readelf'
apk=root/'unity/TestResults/Android/LivLivVerification.apk';out=root/'unity/TestResults/android-apk-audit';out.mkdir(exist_ok=True)
def run(*args):return subprocess.check_output([str(a) for a in args],text=True,stderr=subprocess.STDOUT)
required={'libunity.so','libil2cpp.so','liblivlivwebp.so','libmediapipe_jni.so','libopencv_java4.so','libc++_shared.so'}
evidence={'sha256':hashlib.sha256(apk.read_bytes()).hexdigest(),'size':apk.stat().st_size,'libraries':{}}
with zipfile.ZipFile(apk) as z:
 for source in ['unity/Packages/ai.livliv.player/Browser~/face_landmarker.task','unity/Assets/Verification/Resources/LivLivVerification.bytes']:
  payload=(root/source).read_bytes()
  matches=[n for n in z.namelist() if n.startswith('assets/bin/Data/') and z.getinfo(n).file_size>=len(payload) and payload in z.read(n)]
  assert matches,('Packed Resources missing exact bytes',source)
  evidence.setdefault('resources',{})[source]=matches
 libs=[n for n in z.namelist() if n.startswith('lib/') and n.endswith('.so')]
 assert {n.split('/')[1] for n in libs}=={'arm64-v8a'},libs
 assert required <= {Path(n).name for n in libs},libs
 for n in libs:
  p=out/Path(n).name;p.write_bytes(z.read(n));header=run(readelf,'-h',p);assert 'AArch64' in header,header
  segments=run(readelf,'-lW',p);loads=[l for l in segments.splitlines() if l.strip().startswith('LOAD ')]
  alignment=[int(l.split()[-1],16) for l in loads];assert alignment and min(alignment)>=16384,(n,alignment)
  needed=re.findall(r'Shared library: \[(.*?)\]',run(readelf,'-d',p))
  evidence['libraries'][Path(n).name]={'sha256':hashlib.sha256(p.read_bytes()).hexdigest(),'loadAlignments':alignment,'needed':needed}
assert 'libc++_shared.so' in evidence['libraries']['libopencv_java4.so']['needed']
symbols=run(readelf,'-Ws',out/'liblivlivwebp.so')
for name in ['WebPGetInfo','WebPDecodeRGBAInto']:assert name in symbols,name
permissions=run(buildtools/'aapt','dump','permissions',apk)
for name in ['android.permission.CAMERA','android.permission.RECORD_AUDIO']:assert name in permissions,permissions
evidence['permissions']=permissions;evidence['badging']=run(buildtools/'aapt','dump','badging',apk)
evidence['zipalign']=run(buildtools/'zipalign','-c','-P','16','-v','4',apk).splitlines()[-1]
assert 'Verification successful' in evidence['zipalign']
evidence['signature']=subprocess.check_output([str(buildtools/'apksigner'),'verify','--verbose','--print-certs',str(apk)],text=True,stderr=subprocess.STDOUT,env={**os.environ,'JAVA_HOME':str(android/'OpenJDK')})
assert 'Verified using v2 scheme (APK Signature Scheme v2): true' in evidence['signature'],evidence['signature']
evidence['packaging']=json.loads(run(sys.executable,root/'scripts/unity/check-apk-packaging.py','--output',root/'unity/TestResults/apk-packaging/latest.json'))
(root/'unity/TestResults/android-apk.json').write_text(json.dumps(evidence,indent=2)+'\n')
print(json.dumps({'apk':str(apk),'bytes':evidence['size'],'libraries':list(evidence['libraries']),'alignment':evidence['zipalign']},indent=2))
