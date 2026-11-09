// Predeclare absolute repository `root` with trailing slash, then run through ego-browser nodejs.
const task=await taskSpace('LivLiv Unity port research'),page=task.page('p1'),fs=await import('node:fs/promises'),zlib=await import('node:zlib'),crypto=await import('node:crypto');
await page.goto('http://127.0.0.1:8769');
const output=[];
for(const [id,lid] of [['d03','expr_shy'],['p02','body'],['q03','body'],['v04','back_hair'],['v04','neck_shadow'],['d01','back_hair'],['d03ft','front_hair']]){
 const row=await page.evaluate(async({id,lid})=>{
 const {readModel,latticeCell}=await import('/src/runtime/format.ts'),{alphaGrid}=await import('/src/runtime/puppet.ts?helpers');
 const raw=await(await fetch('/characters/'+id+'/model.json')).json(),m=readModel(raw),l=m.layers.find(l=>l.id===lid),cell=latticeCell(...m.canvas);
 const base64=a=>{let s='';for(let i=0;i<a.length;i+=16384)s+=String.fromCharCode(...a.slice(i,i+16384));return btoa(s)};
 const images=[],inputs=[];
 for(const path of [l.texture,...(l.frames||[])]){const im=new Image();im.src='/characters/'+id+'/'+(l.webp?path.replace(/\.png$/,'.webp'):path);await im.decode();images.push(im);const c=document.createElement('canvas');c.width=im.width;c.height=im.height;const g=c.getContext('2d',{willReadFrequently:true});g.drawImage(im,0,0);const rgba=g.getImageData(0,0,c.width,c.height).data;inputs.push({width:im.width,height:im.height,alpha:base64(Uint8Array.from({length:im.width*im.height},(_,i)=>rgba[4*i+3]))});}
 const gx0=Math.floor(l.x/cell),gy0=Math.floor(l.y/cell),cols=Math.ceil((l.x+l.w)/cell)-gx0,rows=Math.ceil((l.y+l.h)/cell)-gy0;
 const expected=alphaGrid(images,cols,rows,(l.x-gx0*cell)/cell,(l.y-gy0*cell)/cell,l.w/cell,l.h/cell);
 return {id,lid,raw,inputs,expected:base64(expected)};
 },{id,lid});output.push(row);console.log(id+'/'+lid);
}
const sourceNames=new Set(['src/runtime/puppet.ts','src/runtime/format.ts']);
for(const row of output){sourceNames.add('characters/'+row.id+'/model.json');const layer=row.raw.layers.find(l=>l.id===row.lid);for(const p of [layer.texture,...(layer.frames||[])])sourceNames.add('characters/'+row.id+'/'+(layer.webp?p.replace(/\.png$/,'.webp'):p));}
const sourceHashes={};for(const name of sourceNames)sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
const data={sourceHashes,browser:await page.evaluate(()=>navigator.userAgent),samples:output};
await fs.writeFile(root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/alpha-sampling.json.gz',zlib.gzipSync(JSON.stringify(data),{level:9,mtime:0}));
