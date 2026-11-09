// ego-browser: inject absolute root and authorized contextSpaceId. Explicit destructive QA page only.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(contextSpaceId)).page('p1');
await p.goto('http://127.0.0.1:8772/?context-loss='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const initial=await p.evaluate(()=>window.livlivVerification);
if(initial.microphone||initial.tracking||initial.scale!==1)throw Error('Probe requires sample capture off');
await p.evaluate(()=>{
 const canvas=document.querySelector('#unity-canvas');
 window.contextLossRemovals=0;const remove=canvas.removeEventListener;
 canvas.removeEventListener=function(type,...args){if(type==='webglcontextlost')window.contextLossRemovals++;return remove.call(this,type,...args);};
 window.livlivUnity.SendMessage('LivLivVerification','ArmContextLossProbe');
});
await p.waitForFunction(()=>window.livlivContextLoss?.mainCalls===0,undefined,{timeout:10000});
const before=await p.evaluate(()=>window.livlivContextLoss);
if(before.mainLost||before.auxLost||before.mainDisposed||before.auxDisposed)throw Error('Probe armed in wrong state');
const supported=await p.evaluate(()=>{
 const gl=document.querySelector('#unity-canvas').getContext('webgl2');
 window.contextLossGL=gl;const extension=gl.getExtension('WEBGL_lose_context');
 if(!extension)return false;extension.loseContext();return true;
});
if(!supported)throw Error('WEBGL_lose_context unavailable');
await p.waitForFunction(()=>window.livlivContextLoss?.mainCalls===1&&window.livlivContextLoss?.auxCalls===1,undefined,{timeout:10000});
const after=await p.evaluate(()=>({managed:window.livlivContextLoss,actualContextLost:window.contextLossGL.isContextLost(),removals:window.contextLossRemovals,browser:navigator.userAgent}));
if(!after.actualContextLost||after.managed.disposedCalls||!after.managed.mainLost||!after.managed.auxLost||!after.managed.mainDisposed||!after.managed.auxDisposed||after.removals!==1)throw Error('Real context loss failed '+JSON.stringify(after));
const report={result:'Passed',method:'WEBGL_lose_context on the live Unity canvas',initial,before,after,sourceHashes:{},buildHashes:{}};
for(const name of ['unity/Packages/ai.livliv.player/Runtime/base.cs','unity/Packages/ai.livliv.player/Runtime/loading.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLContextLoss.cs','unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivContext.jslib','unity/Assets/Verification/PortVerification.ContextLoss.cs'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))report.buildHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/WebGL/Build/'+name)).digest('hex');
await fs.writeFile(root+'unity/TestResults/context-loss/webgl/browser.json',JSON.stringify(report,null,2)+'\n');
console.log(JSON.stringify({result:report.result,...after}));
// Loss is sticky for the old players. Reload this same QA page to restore a fresh Unity runtime.
await p.goto('http://127.0.0.1:8772/?context-recovered='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
console.log(await p.evaluate(()=>window.livlivVerification));
