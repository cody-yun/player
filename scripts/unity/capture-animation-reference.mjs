// Run with ego-browser nodejs and an injected absolute root. Uses the unmodified upstream renderer.
const task=await taskSpace('LivLiv Unity port research'),page=task.page('p1');
await page.goto('http://127.0.0.1:8769');
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const data={sourceHashes:{},browser:await page.evaluate(()=>navigator.userAgent),cases:[]};
for(const name of ['src/runtime/puppet.ts','src/runtime/params.ts','src/runtime/format.ts'])data.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
const poses=[{name:'turn',frames:30,params:{angleX:24,angleY:-18,angleZ:14,breath:.7,browLY:.5,browRY:-.4}},
 {name:'blink',frames:30,params:{eyeLOpen:.12,eyeROpen:.3,eyeBallX:1,eyeBallY:.5}},
 {name:'talk',frames:30,params:{mouthOpen:.8,vowel:-1,smile:1,angleX:-14,eyeBallX:-.8}},
 {name:'neutral',frames:0,params:{}}];
for(const id of ['d01','char06','d03ft']){
 const name='characters/'+id+'/model.json',bytes=await fs.readFile(root+name),model=JSON.parse(bytes);data.sourceHashes[name]=crypto.createHash('sha256').update(bytes).digest('hex');
 const paths=model.layers.flatMap(l=>[l.texture,...(l.frames||[]),l.mask,l.talk,l.brow_tex].filter(Boolean).map(p=>l.webp?p.replace(/\.png$/,'.webp'):p));if(model.keyforms)paths.push(model.keyforms);
 for(const path of new Set(paths)){const key='characters/'+id+'/'+path;data.sourceHashes[key]=crypto.createHash('sha256').update(await fs.readFile(root+key)).digest('hex')}
 const result=await page.evaluate(async({id,poses})=>{
  const {Puppet,defaultParams}=await import('/src/runtime/puppet.ts'),{Mark}=await import('/src/mark.ts');
  const canvas=document.createElement('canvas');canvas.width=512;canvas.height=768;document.querySelector('#views').replaceChildren(canvas);
  const puppet=new Puppet(canvas);puppet.size=[512,768];const mark=new Mark(puppet.gl),output=[];
  try{
   await puppet.load('/characters/'+id+'/model.json',{pack:false});
   const m=puppet.model;mark.set(m.meta?m.meta.watermark?.kind??'none':m.watermark==='LivLiv'?'livliv':m.watermark?'text':'none',m.watermark??null,m.meta?.watermark?.center!==false);
   for(const pose of poses){
    puppet.params={...defaultParams(),...pose.params};puppet.time=0;puppet.smileShown=0;puppet.resetMouth();puppet.talkLeft=0;puppet.resetPhysics();
    for(const l of puppet.layers){l.out.fill(0);l.outB?.fill(0);l.soft=0}
    if(!pose.frames)puppet.update(0);else for(let i=0;i<pose.frames;i++)puppet.update(1/60);
    puppet.render();mark.draw(0,1);
    const layers=puppet.layers.map(l=>{
     const n=l.rest.length/2,indices=n===0?[]:[...new Set(Array.from({length:33},(_,i)=>Math.floor(i*(n-1)/32)))].filter(i=>i>=0);
     return {id:l.def.id,shown:puppet.shown(l),count:n,indices,output:indices.flatMap(i=>[l.out[2*i],l.out[2*i+1]]),fol:l.fol?indices.map(i=>l.fol[i]):null,folB:l.folB?indices.map(i=>l.folB[i]):null,pivY:l.pivY?indices.map(i=>l.pivY[i]):null,pivC:l.pivC??null,soft:l.soft};
    });
    output.push({id,...pose,layers,nasal:puppet.nasal,png:canvas.toDataURL('image/png').split(',')[1]});
   }
   return output;
  }finally{mark.dispose();puppet.dispose()}
 },{id,poses});
 for(const row of result){await fs.writeFile(root+'unity/TestResults/browser-animated-'+id+'-'+row.name+'.png',Buffer.from(row.png,'base64'));delete row.png;data.cases.push(row)}
 console.log(id+': '+result.length+' original animation frames captured');
}
await fs.writeFile(root+'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/animation-reference.json',JSON.stringify(data)+'\n');
console.log(await page.snapshot());
