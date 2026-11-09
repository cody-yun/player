// ego-browser: inject absolute root and authorized surfaceSpaceId; no new space or device capture.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(surfaceSpaceId)).page('p1');await p.goto('http://127.0.0.1:8773/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const evidence={at:new Date().toISOString(),browser:await p.evaluate(()=>navigator.userAgent),states:{}};
const send=(method,value='')=>p.evaluate(({method,value})=>window.livlivUnity.SendMessage('LivLivVerification',method,value),{method,value});
const wait=fn=>p.waitForFunction(fn,undefined,{timeout:10000});
const read=()=>p.evaluate(()=>window.livlivSurface);
async function configure(name,width,height,scale=1,cap=2,pixels=4194304){
 await p.evaluate(()=>{delete window.livlivSurfacePng;});
 await send('ConfigureSurfaceProbe',JSON.stringify({width,height,scale,cap,pixels}));
 await wait(()=>window.livlivSurface?.targets===(window.livlivSurface.width?1:0)&&window.livlivSurface.materials===1);
 const state=await read();if(state.error||state.time!==0||state.playing||state.scale!==1)throw Error('Surface state failed: '+JSON.stringify(state));
 if(width&&height){
  if(state.width*state.height>pixels||state.width<1||state.height<1)throw Error('Budget failed');
  if(!state.limited){const ratio=Math.min(cap,state.density||1);if(Math.abs(state.width-Math.round(width*ratio))>1||Math.abs(state.height-Math.round(height*ratio))>1)throw Error('Density mismatch');}
  await wait(()=>!!window.livlivSurfacePng);
  const snapshot=await p.evaluate(async()=>{const url=window.livlivSurfacePng;const im=new Image();im.src=url;await im.decode();const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const g=c.getContext('2d');g.drawImage(im,0,0);const bytes=g.getImageData(0,0,c.width,c.height).data;let active=0;for(let i=3;i<bytes.length;i+=4)if(bytes[i])active++;return {url,width:im.width,height:im.height,active};});
  if(snapshot.width!==state.width||snapshot.height!==state.height||snapshot.active<100)throw Error('Snapshot did not render model');
  await fs.writeFile(root+'unity/TestResults/webgl-surface-'+name+'.png',Buffer.from(snapshot.url.split(',')[1],'base64'));state.active=snapshot.active;
 }
 evidence.states[name]=state;return state;
}
try{
 await send('BeginSurfaceProbe');await wait(()=>window.livlivSurface?.targets===1);
 const initial=await configure('portrait',320,480);const reused=await configure('reused',320,480);
 if(initial.targetId!==reused.targetId)throw Error('Same-size target was reallocated');
 const landscape=await configure('landscape',480,320);if(landscape.targetId===initial.targetId)throw Error('Changed target was not replaced');
 await configure('density',320,480,3,2);const budget=await configure('budget',320,480,3,2,10000);if(!budget.limited)throw Error('Budget did not clamp');
 const zero=await configure('zero',0,480);if(zero.width||zero.height||zero.targets)throw Error('Zero-area target retained');
 await configure('recovered',320,480);
 await send('EndSurfaceProbe');await wait(()=>window.livlivSurface.targets===0&&window.livlivSurface.materials===0);evidence.states.disposed=await read();
 evidence.sourceHashes={};
 for(const name of ['src/runtime/puppet.ts','unity/Packages/ai.livliv.player/Runtime/Platform/SurfaceSize.cs','unity/Packages/ai.livliv.ugui/Runtime/PlayerSurface.cs','unity/Packages/ai.livliv.ugui/Runtime/Resources/LivLivSurface.shader','unity/Assets/Verification/PortVerification.Surface.cs'])evidence.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
 await fs.writeFile(root+'unity/TestResults/webgl-surface.json',JSON.stringify(evidence,null,2)+'\n');console.log(evidence);
}finally{await send('EndSurfaceProbe');}
