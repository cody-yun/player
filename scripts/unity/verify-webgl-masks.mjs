// ego-browser: inject absolute root and authorized maskSpaceId. No input devices or new TaskSpace.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(maskSpaceId)).page('p1');await p.goto('http://127.0.0.1:8773/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const report={at:new Date().toISOString(),browser:await p.evaluate(()=>navigator.userAgent),states:[],sourceHashes:{}};
for(const pose of ['glance','halfblink','combined','offscreen']){
 await p.evaluate(pose=>{delete window.livlivSnapshot;window.livlivUnity.SendMessage('LivLivVerification','MaskFrame',pose);},pose);
 await p.waitForFunction(()=>!!window.livlivSnapshot,undefined,{timeout:15000});
 const r=await p.evaluate(()=>window.livlivSnapshot);
 if(r.error||r.scale!==1||r.maskWidth<=0||r.maskHeight<=0||r.maskWidth%64||r.maskHeight%64||r.maskWidth*r.maskHeight>=r.width*r.height)throw Error('Mask storage failed '+JSON.stringify({...r,raw:undefined}));
 if(pose==='offscreen'?r.maskChannels!==0:r.maskChannels<1)throw Error('Mask channel selection failed '+JSON.stringify({...r,raw:undefined}));
 await fs.writeFile(root+'unity/TestResults/webgl-mask-'+pose+'.png',Buffer.from(r.raw,'base64'));delete r.raw;delete r.png;
 report.states.push({pose,...r,storageBytes:r.maskWidth*r.maskHeight*4,previousFullSizeBytes:r.width*r.height*4});
}
for(const name of ['src/runtime/puppet.ts','unity/Packages/ai.livliv.player/Runtime/Platform/MaskBounds.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/PuppetRenderer.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/PuppetRenderer.Draw.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivLayer.shader','unity/Assets/Verification/PortVerification.Snapshot.cs'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
await fs.writeFile(root+'unity/TestResults/webgl-mask.json',JSON.stringify(report,null,2)+'\n');console.log(report);
