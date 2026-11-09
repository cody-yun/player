// ego-browser: inject absolute `root` and authorized `snapshotSpaceId`; never creates a space.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(snapshotSpaceId)).page('p1');
await p.goto('http://127.0.0.1:8769/');
const reference=await p.evaluate(async()=>{
 const {PlayerBase}=await import('/src/base.ts');const {defaultParams}=await import('/src/runtime/params.ts');
 const c=document.createElement('canvas');c.width=c.height=2;
 const gl=c.getContext('webgl2',{alpha:true,premultipliedAlpha:true,preserveDrawingBuffer:true,antialias:false});
 gl.enable(gl.SCISSOR_TEST);
 const colors=[[0,0,255,255],[5,7,9,0],[128,0,0,128],[0,64,0,128]];
 for(let i=0;i<4;i++){gl.scissor(i%2,Math.floor(i/2),1,1);gl.clearColor(...colors[i].map(x=>x/255));gl.clear(gl.COLOR_BUFFER_BIT);}
 class Surface extends PlayerBase{
  constructor(){super(c,defaultParams(),{autoplay:false,idle:false});}
  get drawable(){return false;}get gl(){return gl;}free(){}
 }
 const player=new Surface();let png;
 try{png=player.snapshot();}finally{player.dispose();}
 const image=new Image();image.src=png;await image.decode();const decoded=document.createElement('canvas');decoded.width=decoded.height=2;
 const g=decoded.getContext('2d');g.drawImage(image,0,0);const topDown=Array.from(g.getImageData(0,0,2,2).data);
 gl.getExtension('WEBGL_lose_context')?.loseContext();
 return {png,topDown};
});
const expected=[255,0,0,128,0,128,0,128,0,0,255,255,0,0,0,0];
if(JSON.stringify(reference.topDown)!==JSON.stringify(expected))throw Error('Upstream straight alpha differs: '+JSON.stringify(reference.topDown));
await fs.writeFile(root+'unity/TestResults/snapshot-upstream-alpha.png',Buffer.from(reference.png.split(',')[1],'base64'));
await p.goto('http://127.0.0.1:8772/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const evidence={at:new Date().toISOString(),browser:await p.evaluate(()=>navigator.userAgent),reference:{topDown:reference.topDown},poses:[]};
for(const pose of ['neutral','turn','blink','talk']){
 await p.evaluate(pose=>{delete window.livlivSnapshot;window.livlivUnity.SendMessage('LivLivVerification','SnapshotFrame',pose);},pose);
 await p.waitForFunction(()=>!!window.livlivSnapshot,undefined,{timeout:15000});
 const result=await p.evaluate(async()=>{
  const r=window.livlivSnapshot;if(r.error)return r;
  async function pixels(src){const im=new Image();im.src=src;await im.decode();const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const g=c.getContext('2d');g.drawImage(im,0,0);return g.getImageData(0,0,c.width,c.height).data;}
  const straight=await pixels(r.png),raw=await pixels('data:image/png;base64,'+r.raw);
  // Canvas internally premultiplies both decoded images. Low-alpha rounding in the raw PNG readback is unavoidable.
  let maxRoundtrip=0,alphaDiff=0,active=0,partial=0,mark=0;
  for(let i=0;i<straight.length;i+=4){const a=straight[i+3];alphaDiff=Math.max(alphaDiff,Math.abs(a-raw[i+3]));if(a)active++;if(a&&a<255)partial++;
   if(a>=128)for(let k=0;k<3;k++)maxRoundtrip=Math.max(maxRoundtrip,Math.abs(straight[i+k]*a/255-raw[i+k]));
   const pixel=i/4;if(a&&Math.floor(pixel/r.width)>r.height-80&&pixel%r.width>400)mark++;
  }
  return {...r,maxRoundtrip,alphaDiff,active,partial,mark};
 });
 if(result.error||result.before!==result.after||result.playing||result.visible||!result.activeRestored||result.scale!==1||result.width!==512||result.height!==768||result.alphaDiff||result.maxRoundtrip>2||result.active<10000||result.partial<100||result.mark<100)throw Error('Snapshot failed '+pose+': '+JSON.stringify({...result,png:undefined,raw:undefined}));
 await fs.writeFile(root+'unity/TestResults/webgl-snapshot-'+pose+'.png',Buffer.from(result.png.split(',')[1],'base64'));
 await fs.writeFile(root+'unity/TestResults/webgl-snapshot-'+pose+'-raw.png',Buffer.from(result.raw,'base64'));
 delete result.png;delete result.raw;evidence.poses.push({pose,...result});
}
evidence.sourceHashes={};
for(const name of ['src/base.ts','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.Snapshot.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/Runtime/base.cs','unity/Assets/Verification/PortVerification.Snapshot.cs','unity/Assets/Verification/Probe.jslib'])evidence.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','SetPose','neutral'));
await fs.writeFile(root+'unity/TestResults/webgl-snapshot.json',JSON.stringify(evidence,null,2)+'\n');
console.log(evidence);
