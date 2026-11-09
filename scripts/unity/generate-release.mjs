// Execute the actual upstream release module with a deterministic task queue.
import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {releaseCanvas,claimCanvas} from '../../src/release.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));let next=0,reject=false;const timers=new Map();
globalThis.setTimeout=fn=>{if(reject){reject=false;throw Error('schedule-failed')}timers.set(++next,fn);return next};
globalThis.clearTimeout=id=>timers.delete(id);
const definitions=[
 ['normal',['claim:a','release:a','flush','claim:a']],
 ['cancel',['release:a','claim:a','flush','claim:a']],
 ['reschedule',['release:a','claim:a','release:a','flush','claim:a']],
 ['duplicate',['release:a','release:a','flush','release:a','flush','claim:a']],
 ['no-context',['null:a','flush','claim:a','release:a','flush','claim:a']],
 ['no-extension',['missing:a','flush','claim:a']],
 ['independent',['release:a','release:b','claim:a','flush','claim:a','claim:b']],
 ['claim-many',['claim:a','claim:a','release:a','claim:a','claim:a','flush','claim:a']],
 ['failure',['throw:a','flush','claim:a','release:a','flush']],
 ['schedule-failure',['reject','release:a','release:a','flush','claim:a']],
];
const cases=[];
for(const [name,operations] of definitions){
 const canvases={a:{},b:{}},lost={a:0,b:0},rows=[];timers.clear();reject=false;
 for(const operation of operations){
  const [kind,key]=operation.split(':');let error=null;
  try{
   if(kind==='reject')reject=true;
   else if(kind==='claim')claimCanvas(canvases[key]);
   else if(kind==='flush'){const tasks=[...timers.values()];timers.clear();for(const task of tasks)try{task()}catch(e){error=e.code??e.message}}
   else releaseCanvas(canvases[key],kind==='null'?null:{getExtension:()=>kind==='missing'?null:{loseContext(){lost[key]++;if(kind==='throw')throw Error('backend-failed')}}});
  }catch(e){error=e.code??e.message}
  rows.push({operation,error,lost:{...lost}});
 }
 cases.push({name,rows});
}
const data={sourceHashes:Object.fromEntries(['src/release.ts','src/errors.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),cases};
const dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/release.json',output=JSON.stringify(data,null,2)+'\n';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('release fixture differs');console.log('release fixture matches upstream')}
else{fs.writeFileSync(dest,output);console.log('release: '+cases.length+' upstream task sequences')}
