// Use the same NDK as the Unity player, not a system-wide Android toolchain.
import fs from 'node:fs/promises';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url)),editor=process.argv[2]||'/Applications/Unity/Hub/Editor/6000.3.14f1';
const ndk=editor+'/PlaybackEngines/AndroidPlayer/NDK',packageRoot=root+'unity/Packages/ai.livliv.mediapipe/';
const manifest=JSON.parse(await fs.readFile(packageRoot+'ThirdParty/ndk-runtime.json','utf8'));
const bytes=await fs.readFile(ndk+'/toolchains/llvm/prebuilt/darwin-x86_64/sysroot/usr/lib/aarch64-linux-android/libc++_shared.so');
const sha=crypto.createHash('sha256').update(bytes).digest('hex');if(sha!==manifest.sha256)throw Error('NDK C++ runtime differs from pinned artifact; review Unity/NDK versions');
await fs.mkdir(packageRoot+'Runtime/Plugins/Android/arm64-v8a',{recursive:true});await fs.writeFile(packageRoot+manifest.artifact,bytes);
await fs.copyFile(ndk+'/NOTICE.toolchain',packageRoot+'ThirdParty/NDK-NOTICE.txt');console.log({sha256:sha,artifact:manifest.artifact});
