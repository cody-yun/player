// Copy the pinned npm distribution and task model into the Unity browser adapter.
import fs from 'node:fs/promises';import crypto from 'node:crypto';import {execFileSync} from 'node:child_process';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url)),cache=root+'unity/TestResults/mediapipe/',dest=root+'unity/Packages/ai.livliv.player/Browser~/';await fs.mkdir(cache,{recursive:true});
const archive=cache+'mediapipe-tasks-vision-1.1.1.tgz';
// npm's exact version integrity (not a mutable latest URL).
const expected='jwntgT5tzDrKOkqg6xNDNpRcTUnGvmylZE5DFnzPXossiWOIDdZX+C81LI75hlXhsG58YZW+lGzcmjH1qrQE/Q==';
try{await fs.access(archive)}catch{const r=await fetch('https://registry.npmjs.org/@mediapipe/tasks-vision/-/tasks-vision-1.1.1.tgz');if(!r.ok)throw Error(r.status);await fs.writeFile(archive,Buffer.from(await r.arrayBuffer()));}
if(crypto.createHash('sha512').update(await fs.readFile(archive)).digest('base64')!==expected)throw Error('MediaPipe npm archive integrity differs');
execFileSync('tar',['-xzf',archive,'-C',cache]);await fs.mkdir(dest,{recursive:true});
await fs.copyFile(cache+'package/vision_bundle.mjs',dest+'vision_bundle.mjs');await fs.mkdir(dest+'wasm',{recursive:true});for(const name of ['vision_wasm_internal','vision_wasm_nosimd_internal'])for(const ext of ['js','wasm'])await fs.copyFile(cache+'package/wasm/'+name+'.'+ext,dest+'wasm/'+name+'.'+ext);
const modelUrl='https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task';
try{await fs.access(cache+'face_landmarker.task')}catch{const r=await fetch(modelUrl);if(!r.ok)throw Error(r.status);await fs.writeFile(cache+'face_landmarker.task',Buffer.from(await r.arrayBuffer()));}
const manifestPath=root+'unity/Packages/ai.livliv.player/ThirdParty/mediapipe-webgl.json';let old;try{old=JSON.parse(await fs.readFile(manifestPath,'utf8'))}catch(error){if(error.code!=='ENOENT')throw error;}
const modelHash=crypto.createHash('sha256').update(await fs.readFile(cache+'face_landmarker.task')).digest('hex');if(old&&old.model.sha256!==modelHash)throw Error('Face model differs from pinned hash');
await fs.copyFile(cache+'face_landmarker.task',dest+'face_landmarker.task');const files={};
for(const path of ['vision_bundle.mjs','face_landmarker.task',...(await fs.readdir(dest+'wasm')).map(n=>'wasm/'+n)])files[path]=crypto.createHash('sha256').update(await fs.readFile(dest+path)).digest('hex');
if(old&&JSON.stringify(old.files)!==JSON.stringify(files))throw Error('MediaPipe files differ from pinned manifest');
const manifest={package:'@mediapipe/tasks-vision',version:'1.1.1',npmIntegrity:'sha512-'+expected,license:'Apache-2.0',source:'https://github.com/google-ai-edge/mediapipe',model:{url:modelUrl,sha256:modelHash},files};
if(!old)await fs.writeFile(manifestPath,JSON.stringify(manifest,null,2)+'\n');console.log({files:Object.keys(files),modelHash});
