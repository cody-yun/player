// Original WebGL output at the same four static poses as PortVerification.CaptureFrame.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');const p=(await taskSpace(3)).page('p1');await p.goto('http://127.0.0.1:8769/');
const rows=await p.evaluate(async()=>{
 const {Puppet,defaultParams}=await import('/src/runtime/puppet.ts'),{Mark}=await import('/src/mark.ts');const c=document.createElement('canvas');c.width=512;c.height=768;document.querySelector('#views').replaceChildren(c);const puppet=new Puppet(c),mark=new Mark(puppet.gl);puppet.size=[512,768];const rows=[];
 try{
  await puppet.load('/characters/char06/model.json',{pack:false});const m=puppet.model;mark.set(m.meta?m.meta.watermark?.kind??'none':m.watermark==='LivLiv'?'livliv':m.watermark?'text':'none',m.watermark??null,m.meta?.watermark?.center!==false);
  for(const name of ['neutral','turn','blink','talk']){
   puppet.params={...defaultParams(),angleX:name==='turn'?20:0,angleY:name==='turn'?-10:0,eyeLOpen:name==='blink'?.2:1,eyeROpen:name==='blink'?.2:1,mouthOpen:name==='talk'?.8:0};puppet.time=0;puppet.smileShown=0;puppet.resetMouth();puppet.talkLeft=0;puppet.resetPhysics();puppet.update(0);puppet.render();mark.draw(0,1);
   rows.push({name,png:c.toDataURL('image/png').split(',')[1]});
  }
 }finally{mark.dispose();puppet.dispose();}return rows;
});
for(const r of rows)await fs.writeFile(root+'unity/TestResults/browser-webgl-char06-'+r.name+'.png',Buffer.from(r.png,'base64'));
const hashes={};for(const path of ['src/runtime/puppet.ts','src/runtime/params.ts','src/mark.ts','characters/char06/model.json'])hashes[path]=crypto.createHash('sha256').update(await fs.readFile(root+path)).digest('hex');
await fs.writeFile(root+'unity/TestResults/webgl-reference.json',JSON.stringify({hashes,poses:rows.map(r=>r.name)},null,2));console.log({poses:rows.map(r=>r.name),hashes});
