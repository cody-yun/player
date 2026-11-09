"""Build decoder-only ARM64/x64 slices with Apple Clang and combine/sign a universal dylib."""
from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile

root=Path(__file__).resolve().parents[2]
cache=root/'unity/TestResults/webp-macos';cache.mkdir(parents=True,exist_ok=True)
archive=root/'unity/TestResults/webp-webgl/libwebp-1.6.0.tar.gz'
source_url='https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz'
source_sha='e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564'
def run(*args):return subprocess.check_output(list(map(str,args)),text=True,stderr=subprocess.STDOUT)
def sha(file):return hashlib.sha256(file.read_bytes()).hexdigest()
if not archive.exists():
    archive.parent.mkdir(parents=True,exist_ok=True)
    subprocess.run(['curl','--fail','--location',source_url,'--output',str(archive)],check=True)
assert sha(archive)==source_sha,'Pinned libwebp source differs'
with tarfile.open(archive) as tar:tar.extractall(cache,filter='data')
sdk=run('xcrun','--sdk','macosx','--show-sdk-path').strip()
clang=run('xcrun','--find','clang').strip()
common=['-DCMAKE_BUILD_TYPE=Release','-DBUILD_SHARED_LIBS=ON','-DWEBP_BUILD_LIBWEBPDECODER=ON',
        '-DCMAKE_OSX_DEPLOYMENT_TARGET=13.0','-DCMAKE_OSX_SYSROOT='+sdk,'-DCMAKE_C_COMPILER='+clang,
        '-DCMAKE_C_FLAGS_RELEASE=-O3 -DNDEBUG -ffile-prefix-map='+str(cache)+'=.']
common += ['-DWEBP_BUILD_'+s+'=OFF' for s in ['ANIM_UTILS','CWEBP','DWEBP','GIF2WEBP','IMG2WEBP','VWEBP','WEBPINFO','LIBWEBPMUX','WEBPMUX','EXTRAS']]
slices=[]
for arch in ['arm64','x86_64']:
    build=cache/arch
    (cache/(arch+'-configure.log')).write_text(run('cmake','-S',cache/'libwebp-1.6.0','-B',build,*common,'-DCMAKE_OSX_ARCHITECTURES='+arch))
    (cache/(arch+'-build.log')).write_text(run('cmake','--build',build,'--target','webpdecoder','--parallel','4'))
    thin=cache/('liblivlivwebp-'+arch+'.dylib');shutil.copyfile(build/'libwebpdecoder.dylib',thin)
    run('install_name_tool','-id','@rpath/liblivlivwebp.dylib',thin)
    assert run('lipo','-archs',thin).strip()==arch
    slices.append(thin)
artifact=cache/'liblivlivwebp.dylib'
run('lipo','-create',*slices,'-output',artifact)
run('codesign','--force','--sign','-','--identifier','ai.livliv.webp',artifact)
run('codesign','--verify','--strict',artifact)
assert set(run('lipo','-archs',artifact).split())=={'arm64','x86_64'}
slice_hashes={}
for arch in ['arm64','x86_64']:
    thin=cache/('signed-'+arch+'.dylib');run('lipo',artifact,'-thin',arch,'-output',thin);slice_hashes[arch]=sha(thin)
data={'library':'libwebp decoder','version':'1.6.0','source':source_url,'sourceSha256':source_sha,
      'artifactSha256':sha(artifact),'architectures':['arm64','x86_64'],'sliceSha256':slice_hashes,
      'deploymentTarget':'13.0','xcode':run('xcodebuild','-version').strip(),'sdk':run('xcrun','--sdk','macosx','--show-sdk-version').strip(),
      'compiler':run(clang,'--version').splitlines()[0],'cmake':run('cmake','--version').splitlines()[0],
      'installName':'@rpath/liblivlivwebp.dylib','signature':'ad hoc, identifier ai.livliv.webp',
      'license':'libwebp-LICENSE.md','previousProvenance':'libwebp.json records the historical ARM64 Homebrew artifact'}
package=root/'unity/Packages/ai.livliv.player';manifest=package/'ThirdParty/libwebp-macos-universal.json'
if manifest.exists() and json.loads(manifest.read_text())['artifactSha256']!=data['artifactSha256']:
    raise SystemExit('Universal decoder differs from pinned artifact; review toolchain/signature before updating provenance')
shutil.copyfile(artifact,package/'Plugins/macOS/liblivlivwebp.dylib')
if not manifest.exists():manifest.write_text(json.dumps(data,indent=2)+'\n')
(cache/'provenance.json').write_text(json.dumps(data,indent=2)+'\n')
print(json.dumps(data,indent=2))
