// Restore the exact upstream native UPM release used by this verification project.
import fs from 'node:fs/promises';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url));
const name='com.github.homuler.mediapipe-0.16.3.tgz',sha='cc3e77a219e0b99618ae3be64c31a566197deedc69c1e136acf52d65d7cf2e79';
const url='https://github.com/homuler/MediaPipeUnityPlugin/releases/download/v0.16.3/'+name,path=root+'unity/ThirdParty/'+name;
await fs.mkdir(root+'unity/ThirdParty',{recursive:true});let bytes;
try{bytes=await fs.readFile(path)}catch(e){if(e.code!=='ENOENT')throw e;const r=await fetch(url);if(!r.ok)throw Error('MediaPipe download '+r.status);bytes=Buffer.from(await r.arrayBuffer());}
if(crypto.createHash('sha256').update(bytes).digest('hex')!==sha)throw Error('MediaPipe native archive differs from pinned release SHA-256');
await fs.writeFile(path,bytes);console.log({path,sha256:sha,bytes:bytes.length});
