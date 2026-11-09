// Run through ego-browser nodejs, with an absolute `root` (repository path + slash) declared first.
// See docs/unity/GRAPHICS.md. Keeps the current goal's TaskSpace.
const task=await taskSpace('LivLiv Unity port research'),page=task.page('p1');
await page.goto('http://127.0.0.1:8769');
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const data={sourceHashes:{},models:[]};
for(const name of ['src/runtime/puppet.ts','src/runtime/format.ts'])data.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
data.browser=await page.evaluate(()=>navigator.userAgent);
for(const id of (await fs.readdir(root+'characters')).sort()){
 try {await fs.access(root+'characters/'+id+'/model.json')}catch{continue}
 const source='characters/'+id+'/model.json';data.sourceHashes[source]=crypto.createHash('sha256').update(await fs.readFile(root+source)).digest('hex');
 const result=await page.evaluate(async id=>{
  const {alphaGrid}=await import('/src/runtime/puppet.ts?helpers');const {readModel,latticeCell}=await import('/src/runtime/format.ts');
  const m=readModel(await(await fetch('/characters/'+id+'/model.json')).json()),cell=latticeCell(...m.canvas),layers=[];
  for(const l of m.layers){
   const imgs=await Promise.all([l.texture,...(l.frames||[])].map(p=>new Promise((resolve,reject)=>{const im=new Image();im.onload=()=>resolve(im);im.onerror=reject;im.src='/characters/'+id+'/'+(l.webp?p.replace(/\.png$/,'.webp'):p)})));
   const gx0=Math.floor(l.x/cell),gy0=Math.floor(l.y/cell),cols=Math.ceil((l.x+l.w)/cell)-gx0,rows=Math.ceil((l.y+l.h)/cell)-gy0;
   const occ=alphaGrid(imgs,cols,rows,(l.x-gx0*cell)/cell,(l.y-gy0*cell)/cell,l.w/cell,l.h/cell);
   layers.push({id:l.id,cols,rows,occ:btoa(String.fromCharCode(...occ))});
  }
  return {id,layers};
 },id);
 data.models.push(result);console.log(id+': '+result.layers.length+' alpha grids');
}
await fs.writeFile(root+'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/alpha-reference.json',JSON.stringify(data,null,2)+'\n');
console.log(await page.snapshot());
