"""Runs inside the pinned x86_64 Docker toolchain; writes only /out."""
from pathlib import Path
import hashlib,json,shutil,subprocess,tarfile
source=Path('/repo/unity/TestResults/webp-webgl/libwebp-1.6.0.tar.gz');out=Path('/out')
source_sha='e4ab7009bf0629fd11982d4c2aa83964cf244cffba7347ecd39019a9e38c4564'
assert hashlib.sha256(source.read_bytes()).hexdigest()==source_sha,'Pinned libwebp source differs'
with tarfile.open(source) as archive:archive.extractall(out,filter='data')
src=out/'libwebp-1.6.0'
common=['-DCMAKE_BUILD_TYPE=Release','-DBUILD_SHARED_LIBS=ON','-DWEBP_BUILD_LIBWEBPDECODER=ON']
common += ['-DWEBP_BUILD_'+s+'=OFF' for s in ['ANIM_UTILS','CWEBP','DWEBP','GIF2WEBP','IMG2WEBP','VWEBP','WEBPINFO','LIBWEBPMUX','WEBPMUX','EXTRAS']]
def run(*args):return subprocess.check_output(list(map(str,args)),text=True,stderr=subprocess.STDOUT)
for target in ['linux-x86_64','windows-x86_64']:
    build=out/target;args=[]
    if target.startswith('windows'):
        args=['-DCMAKE_SYSTEM_NAME=Windows','-DCMAKE_C_COMPILER=x86_64-w64-mingw32-gcc-posix',
              '-DCMAKE_RC_COMPILER=x86_64-w64-mingw32-windres','-DCMAKE_SHARED_LINKER_FLAGS=-static-libgcc -Wl,--no-insert-timestamp']
    (out/(target+'-configure.log')).write_text(run('cmake','-S',src,'-B',build,*common,*args))
    (out/(target+'-build.log')).write_text(run('cmake','--build',build,'--target','webpdecoder','--parallel','4'))
    if target.startswith('windows'):
        candidates=list(build.glob('*webpdecoder*.dll'));assert len(candidates)==1,candidates
        artifact=candidates[0];name='livlivwebp.dll';compiler='x86_64-w64-mingw32-gcc-posix'
    else:artifact=build/'libwebpdecoder.so';name='liblivlivwebp.so';compiler='gcc'
    dest=out/(target+'-'+name);shutil.copyfile(artifact,dest)
    report={'version':'1.6.0','source':'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/libwebp-1.6.0.tar.gz',
            'sourceSha256':source_sha,'artifactSha256':hashlib.sha256(dest.read_bytes()).hexdigest(),'target':target,
            'compiler':run(compiler,'--version').splitlines()[0],'cmake':run('cmake','--version').splitlines()[0],
            'baseImage':'ubuntu:24.04@sha256:fe81560990fae86dc4743202b2cb4d3df98d0bcf949173cecef15ce1127ef852',
            'packages':run('dpkg-query','-W','gcc','libc6','cmake','gcc-mingw-w64-x86-64-posix','mingw-w64-common')}
    (out/(target+'.json')).write_text(json.dumps(report,indent=2)+'\n');print(json.dumps(report),flush=True)
