// ego-browser: inject absolute root and authorized loadingSpaceId. Reuse the existing page.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(loadingSpaceId)).page('p1');await p.goto('http://127.0.0.1:8771/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const evidence={at:new Date().toISOString(),browser:await p.evaluate(()=>navigator.userAgent),steps:{}};
let sequence=0;
const start=async path=>{const id=++sequence;await p.evaluate(path=>window.livlivUnity.SendMessage('LivLivVerification','LoadModel',location.origin+path),path);return id;};
const finish=async(id,name,state)=>{
 await p.waitForFunction(id=>window.livlivLoads?.[id]?.state!=='loading'&&!!window.livlivLoads?.[id],id,{timeout:60000});
 const result=await p.evaluate(id=>window.livlivLoads[id],id);evidence.steps[name]=result;
 if(result.state!==state)throw Error(name+': '+JSON.stringify(result));return result;
};
const progress=result=>{
 let share=-1,built=false;for(const p of result.progress){if(p.step==='build'){if(built)throw Error('duplicate build');built=true;}else{if(built||p.progress<=share)throw Error('non-monotonic progress');share=p.progress;}}
 if(!built||share!==1)throw Error('incomplete progress');
};
progress(await finish(await start('/package.zip'),'zip','ready'));
progress(await finish(await start('/models/char06'),'folder','ready'));
progress(await finish(await start('/unknown.zip'),'unknown-length','ready'));
const before=await p.evaluate(async()=>await (await fetch('/counts')).json());
progress(await finish(await start('/retry.zip'),'retry','ready'));
const after=await p.evaluate(async()=>await (await fetch('/counts')).json());
if(after['/retry.zip']-(before['/retry.zip']||0)!==2)throw Error('expected exactly one retry');
for(const [path,name,code] of [['/absent.zip','404','http'],['/bad.zip','bad-package','not-a-model']]){
 const r=await finish(await start(path),name,'failed');if(r.error!==code||!r.keptPrevious)throw Error('failed load replaced model');
}
const cancelled=await start('/slow.zip');
await p.waitForFunction(id=>window.livlivLoads?.[id]?.progress.some(p=>p.progress>0),cancelled,{timeout:15000});
await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','CancelModelLoad'));
const r=await finish(cancelled,'cancel','cancelled');if(!r.keptPrevious||r.endFrame<=r.startFrame)throw Error('cancel did not preserve/render old model');
const replaced=await start('/slow.zip');const replacement=await start('/package.zip');
await finish(replaced,'replaced','cancelled');progress(await finish(replacement,'replacement','ready'));
evidence.player=await p.evaluate(()=>window.livlivVerification);
if(evidence.player.error)throw Error(evidence.player.error);
evidence.sourceHashes={};for(const path of ['unity/Packages/ai.livliv.player/Runtime/loading.cs','unity/Packages/ai.livliv.player/Runtime/Platform/ModelDownload.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityModelTransport.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs'])evidence.sourceHashes[path]=crypto.createHash('sha256').update(await fs.readFile(root+path)).digest('hex');
await fs.writeFile(root+'unity/TestResults/webgl-loading.json',JSON.stringify(evidence,null,2)+'\n');
console.log({steps:Object.keys(evidence.steps),model:evidence.player.model,frames:evidence.player.frames});
