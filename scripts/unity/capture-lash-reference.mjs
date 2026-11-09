// Execute with ego-browser nodejs and an injected absolute root.
const fs=await import('node:fs/promises'),zlib=await import('node:zlib'),crypto=await import('node:crypto');
const t=await taskSpace(3),p=t.page('p1');
const data=await p.evaluate(async()=>{
 const {closedLashLine}=await import('/src/runtime/puppet.ts?helpers'),result=[];
 for(const id of ['d01','char06','d03ft']){
  const m=await(await fetch('/characters/'+id+'/model.json')).json();
  for(const d of m.layers.filter(l=>l.role?.startsWith('lid_'))){
   const im=await new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src='/characters/'+id+'/'+(d.webp?d.texture.replace(/\.png$/,'.webp'):d.texture)});
   const c=document.createElement('canvas');c.width=im.width;c.height=im.height;c.getContext('2d').drawImage(im,0,0);
   const a=c.getContext('2d').getImageData(0,0,c.width,c.height).data;let text='';for(let i=0;i<a.length;i+=8192)text+=String.fromCharCode(...a.subarray(i,i+8192));
   const line=closedLashLine(im,d),xs=Array.from({length:33},(_,i)=>d.x-10+(d.w+20)*i/32);
   result.push({model:id,def:d,width:c.width,height:c.height,rgba:btoa(text),xs,expected:line?xs.map(x=>line(x)):null});
  }
 }
 return result;
});
const sourceHashes={};
for(const path of ['src/runtime/puppet.ts',...data.flatMap(r=>['characters/'+r.model+'/model.json','characters/'+r.model+'/'+(r.def.webp?r.def.texture.replace(/\.png$/,'.webp'):r.def.texture)])])sourceHashes[path]=crypto.createHash('sha256').update(await fs.readFile(root+path)).digest('hex');
await fs.writeFile(root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/lash.json.gz',zlib.gzipSync(JSON.stringify({sourceHashes,samples:data})));console.log(data.map(r=>({id:r.model+'/'+r.def.id,width:r.width,height:r.height})));
