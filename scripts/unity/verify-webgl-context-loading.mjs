// ego-browser: inject absolute root and authorized contextSpaceId; genuine gated HTTP load + context loss.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(contextSpaceId)).page('p1');
const key='context-load-'+Date.now(),resource='/model.zip?id='+key;
await p.goto('http://127.0.0.1:8773/?context-loading='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
await p.evaluate(resource=>{
 const original=window.fetch;window.contextFetch={seen:false,signal:false,aborted:false};
 window.fetch=function(input,options){
  if(String(input?.url||input).includes(resource)){
   const state=window.contextFetch,signal=options?.signal||input?.signal;state.seen=true;state.signal=!!signal;
   if(signal){state.aborted=signal.aborted;signal.addEventListener('abort',()=>{state.aborted=true;});}
  }
  return original.apply(this,arguments);
 };
},resource);
await p.evaluate(resource=>window.livlivUnity.SendMessage('LivLivVerification','ArmContextLossLoadingProbe','http://127.0.0.1:8773'+resource),resource);
await p.waitForFunction(()=>window.livlivContextLoss?.loadState==='loading',undefined,{timeout:10000});
await p.waitForFunction(async resource=>(await (await fetch('/counts')).json())[resource]===1,resource,{timeout:10000});
const before=await p.evaluate(()=>window.livlivContextLoss);
try{
 await p.evaluate(()=>{
  const gl=document.querySelector('#unity-canvas').getContext('webgl2');window.contextLossGL=gl;
  const extension=gl.getExtension('WEBGL_lose_context');if(!extension)throw Error('No context-loss extension');extension.loseContext();
 });
 await p.waitForFunction(()=>window.livlivContextLoss?.loadState==='context-lost',undefined,{timeout:10000});
 const after=await p.evaluate(()=>({managed:window.livlivContextLoss,lost:window.contextLossGL.isContextLost(),network:window.contextFetch,browser:navigator.userAgent}));
 if(!after.lost||after.managed.mainCalls!==1||after.managed.auxCalls!==1||after.managed.disposedCalls||!after.managed.auxDisposed)throw Error(JSON.stringify(after));
 if(!after.network.seen||!after.network.signal||!after.network.aborted)throw Error('HTTP fetch was not aborted '+JSON.stringify(after));
 const evidence={result:'Passed',before,after,resource,sourceHashes:{},buildHashes:{}};
 for(const name of ['unity/Packages/ai.livliv.player/Runtime/base.cs','unity/Packages/ai.livliv.player/Runtime/loading.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityModelTransport.cs','unity/Assets/Verification/PortVerification.ContextLoss.cs'])evidence.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
 for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))evidence.buildHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/WebGL/Build/'+name)).digest('hex');
 await fs.writeFile(root+'unity/TestResults/context-loss/webgl/loading-browser.json',JSON.stringify(evidence,null,2)+'\n');console.log(JSON.stringify(evidence.after));
}finally{
 await p.evaluate(key=>fetch('/release?id='+key).then(r=>r.text()),key);
}
await p.goto('http://127.0.0.1:8773/?context-load-recovered='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
console.log(await p.evaluate(()=>window.livlivVerification));
