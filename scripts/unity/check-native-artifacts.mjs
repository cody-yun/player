import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../unity/Packages/ai.livliv.player/',import.meta.url));
for(const [name,path] of [['libwebp-macos-universal.json','Plugins/macOS/liblivlivwebp.dylib'],['libwebp-webgl.json','Plugins/WebGL/liblivlivwebp.a'],['libwebp-android.json','Plugins/Android/arm64-v8a/liblivlivwebp.so'],['libwebp-ios.json','Plugins/iOS/liblivlivwebp.a'],['libwebp-linux-x86_64.json','Plugins/Linux/x86_64/liblivlivwebp.so'],['libwebp-windows-x86_64.json','Plugins/Windows/x86_64/livlivwebp.dll']]){
 const manifest=JSON.parse(fs.readFileSync(root+'ThirdParty/'+name));const actual=crypto.createHash('sha256').update(fs.readFileSync(root+path)).digest('hex');
 if(actual!==manifest.artifactSha256)throw Error(path+' differs from provenance manifest');console.log(path+': SHA-256 matches');
}
const vision=JSON.parse(fs.readFileSync(root+'ThirdParty/mediapipe-webgl.json'));
for(const [path,hash] of Object.entries(vision.files)){
 const actual=crypto.createHash('sha256').update(fs.readFileSync(root+'Browser~/'+path)).digest('hex');
 if(actual!==hash)throw Error(path+' differs from MediaPipe provenance manifest');
}
console.log('MediaPipe: '+Object.keys(vision.files).length+' pinned asset hashes match');
const nativeRoot=fileURLToPath(new URL('../../unity/ThirdParty/',import.meta.url));
const native=JSON.parse(fs.readFileSync(nativeRoot+'mediapipe-native.json'));
if(crypto.createHash('sha256').update(fs.readFileSync(nativeRoot+native.archive)).digest('hex')!==native.sha256)throw Error('Native MediaPipe tarball differs from provenance manifest');
console.log('Native MediaPipe UPM tarball: pinned SHA-256 matches');

const mediaRoot=fileURLToPath(new URL('../../unity/Packages/ai.livliv.mediapipe/',import.meta.url));
const ndk=JSON.parse(fs.readFileSync(mediaRoot+'ThirdParty/ndk-runtime.json'));
if(crypto.createHash('sha256').update(fs.readFileSync(mediaRoot+ndk.artifact)).digest('hex')!==ndk.sha256)throw Error('Android C++ runtime differs from pinned NDK');
console.log('Android C++ runtime: pinned SHA-256 matches');
