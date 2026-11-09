"""Inspect the unsigned iPhoneOS app after xcodebuild; not a device execution claim."""
from pathlib import Path
import argparse,hashlib,json,plistlib,subprocess,re
root=Path(__file__).resolve().parents[2]
parser=argparse.ArgumentParser()
parser.add_argument('--derived-data',type=Path,default=root/'unity/TestResults/ios-derived')
parser.add_argument('--export',type=Path,default=root/'unity/TestResults/iOS')
parser.add_argument('--output',type=Path,default=root/'unity/TestResults/ios-app.json')
args=parser.parse_args();products=args.derived_data/'Build/Products/Debug-iphoneos'
apps=list(products.glob('*.app'));assert len(apps)==1,apps
app=apps[0];info=plistlib.loads((app/'Info.plist').read_bytes());exe=app/info['CFBundleExecutable']
framework=app/'Frameworks/UnityFramework.framework/UnityFramework';media=app/'Frameworks/MediaPipeUnity.framework/MediaPipeUnity'
def run(*args):return subprocess.check_output([str(a) for a in args],text=True,stderr=subprocess.STDOUT)
assert info['NSCameraUsageDescription'] and info['NSMicrophoneUsageDescription']
minimum=tuple(map(int,info['MinimumOSVersion'].split('.')));assert minimum>=(15,0)
architectures={}
for p in [exe,framework,media]:
 assert p.is_file(),p
 architectures[str(p.relative_to(app))]=run('lipo','-archs',p).strip().split()
 assert architectures[str(p.relative_to(app))]==['arm64'],p
 load=run('otool','-l',p);versions=re.findall(r'\bminos ([0-9.]+)',load)
 assert versions and all(tuple(map(int,v.split('.')))<=minimum for v in versions),(p,versions)
symbols=run('nm','-gU',framework)
for name in ['_WebPGetInfo','_WebPDecodeRGBAInto']:assert name in symbols,name
libraries=run('otool','-L',framework);assert '@rpath/MediaPipeUnity.framework/MediaPipeUnity' in libraries,libraries
assert '_mp_tasks_core_TaskRunner_Create__PKc_i_PF' in run('nm','-gU',media)
assets=(app/'Data/resources.assets').read_bytes()
resources=['unity/Packages/ai.livliv.player/Browser~/face_landmarker.task','unity/Assets/Verification/Resources/LivLivVerification.bytes','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivMarkFont.ttf',
 'unity/Assets/Verification/Resources/LivLivPortrait.bytes','unity/Assets/Verification/Resources/LivLivSpeechMp3.bytes','unity/Assets/Verification/Resources/LivLivSpeechFloat.bytes',
 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-baseline.jpg','unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-progressive.jpg']
for source in resources:
 assert (root/source).read_bytes() in assets,('Packed Resources missing exact bytes',source)
metadata=app/'Data/Managed/Metadata/global-metadata.dat';assert metadata.is_file()
export=args.export/'Data/Managed/Metadata/global-metadata.dat';assert metadata.read_bytes()==export.read_bytes()
e={'app':str(app),'signing':'CODE_SIGNING_ALLOWED=NO; build/link validation only','info':info,'dependencies':libraries,'architectures':architectures,
 'files':{str(p.relative_to(app)):hashlib.sha256(p.read_bytes()).hexdigest() for p in [exe,framework,media,metadata,app/'Data/resources.assets',app/'Data/sharedassets0.assets']},
 'resources':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in resources},'bytes':sum(p.stat().st_size for p in app.rglob('*') if p.is_file())}
e['ownerFontSha256']=hashlib.sha256((root/'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivMarkFont.ttf').read_bytes()).hexdigest()
e['sourceHashes']={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted((root/'unity/Packages/ai.livliv.player/Runtime').rglob('*.cs'))}
e['sourceHashes']['unity/Packages/ai.livliv.ugui/Runtime/PosterPresenter.cs']=hashlib.sha256((root/'unity/Packages/ai.livliv.ugui/Runtime/PosterPresenter.cs').read_bytes()).hexdigest()
for package in ['ai.livliv.ugui','ai.livliv.mediapipe']:
 for p in sorted((root/'unity/Packages'/package/'Runtime').rglob('*.cs')):
  e['sourceHashes'][str(p.relative_to(root))]=hashlib.sha256(p.read_bytes()).hexdigest()
for p in sorted((root/'unity/Assets/Verification').glob('*.cs')):
 e['sourceHashes'][str(p.relative_to(root))]=hashlib.sha256(p.read_bytes()).hexdigest()
p=root/'unity/Packages/ai.livliv.player/compatibility.json'
e['sourceHashes'][str(p.relative_to(root))]=hashlib.sha256(p.read_bytes()).hexdigest()
args.output.parent.mkdir(parents=True,exist_ok=True);args.output.write_text(json.dumps(e,indent=2)+'\n')
print(json.dumps({'app':str(app),'bytes':e['bytes'],'minimumOS':info['MinimumOSVersion'],'embeddedMediaPipe':True,'webpSymbols':True},indent=2))
