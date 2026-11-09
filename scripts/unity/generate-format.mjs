// Golden outputs from unmodified upstream format.ts and zip.ts; Node >=22.18.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { gzipSync, deflateRawSync } from 'node:zlib'
import { fileURLToPath } from 'node:url'
import { readModel, pickLabel, langOf, LIMITS, latticeCell } from '../../src/runtime/format.ts'
import { unzip, isZip } from '../../src/zip.ts'
const root=fileURLToPath(new URL('../../',import.meta.url))
const dest=path.join(root,'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/format.json.gz')
const sourceHashes={}
for(const p of ['src/runtime/format.ts','src/zip.ts','src/errors.ts'])sourceHashes[p]=crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex')
const data={sourceHashes,models:[],labels:[],languages:[],zip:[],lattice:[]}
const clone=x=>JSON.parse(JSON.stringify(x))
function model(name,input,lang='en') {
  const raw=clone(input)
  try { const result=readModel(raw,lang); data.models.push({name,input,lang,result:clone(result),same:result===raw}) }
  catch(e){data.models.push({name,input,lang,error:e.code??null})}
}
for(const dir of fs.readdirSync(path.join(root,'characters'))) {
  const p=`characters/${dir}/model.json`; if(!fs.existsSync(path.join(root,p)))continue
  sourceHashes[p]=crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex')
  model(dir,JSON.parse(fs.readFileSync(path.join(root,p),'utf8')),'ja-JP')
}
const base={format:'livliv-model',version:4,name:'Synthetic',canvas:[64,64],meta:{generator:{name:'test'},watermark:{kind:'none'}},
  anchors:{head:{cx:32,cy:20,rx:12,ry:14},face:[20,8,44,34],neck_y:34,top_y:0,bottom_y:63,hair_root_y:6},
  layers:[{id:'body',texture:'textures/body.png',x:0,y:0,w:64,h:64,group:'body'}],
  expressions:[{id:'smile',label:{en:'Smile',ja:'笑顔',zh:'笑', 'zh-Hant':'微笑'},hotkey:'1'}]}
model('minimal-v4',base)
const legacy={...clone(readModel(clone(base))),format:'livemaker-puppet',version:3}
model('legacy',legacy)
const ancient=clone(legacy);delete ancient.format;delete ancient.version;model('ancient',ancient)
model('null',null);model('array',[])
function change(name,fn,b=base){const x=clone(b);fn(x);model(name,x)}
change('metadata',m=>{m.tuning={turn:.8,hair_front:.3,hair_back:1.2};m.files={keyforms:'keyforms.json','x-thing':1};m.meta.watermark={kind:'text',text:'© Mira'};m.profile={id:'chibi',facing:'front',glasses:true};m.layers[0].label={en:'Body'};m.layers[0].expression='smile'})
change('unknown-feature',m=>m.requires=['mesh-warp'])
change('newer',m=>m.version=5)
change('false-format',m=>m.format='vrm')
change('old-newer',m=>m.version=4,legacy)
change('legacy-string-newer',m=>m.version='4',legacy)
change('requires-false',m=>m.requires=false)
change('null-defaults',m=>{m.anchors.collar_y=null;m.anchors.brows=[null,[1,1,5,5]];m.layers[0].depth=null})
change('unsafe-image',m=>m.layers[0].texture='textures/../../secret.png')
change('unsafe-keyforms',m=>m.files={keyforms:'../outside.json'})
change('too-many-layers',m=>m.layers=Array.from({length:513},(_,i)=>({...m.layers[0],id:'l'+i})))
for(const n of [14,15])change('vertices-'+n,m=>{m.canvas=[768,768];m.layers=Array.from({length:n},(_,i)=>({...m.layers[0],id:'l'+i,x:-768,y:-768,w:2304,h:2304}))})
for(const n of [7,8])change('skinned-'+n,m=>{m.canvas=[768,768];m.layers=Array.from({length:n},(_,i)=>({...m.layers[0],id:'l'+i,x:-768,y:-768,w:2304,h:2304,bones:['a']}))})
change('duplicate-chain',m=>m.layers[0].bones=['a','a'])
change('readback-budget',m=>{m.canvas=[8000,8000];Object.assign(m.layers[0],{id:'brows',x:-1000,y:-1000,w:9000,h:9000})})
change('valid-chain',m=>m.rig={chains:[{id:'c',points:[[1,1],[2,2]],k:[10,20],damp:1,drive:1,hang:0,wind:0,bias:{smile:1}}]})
const paths=['canvas','layers','layers.0.x','layers.0.depth','layers.0.frames','layers.0.pivot','layers.0.role','layers.0.group','layers.0.blush','layers.0.bones','anchors.eyes','anchors.brows','anchors.brow_lines','anchors.head.rx','anchors.face','anchors.jaw','tuning','rig','rig.body','rig.chains','keyforms','expressions']
const nasty=[null,-1,0,1e9,1e308,'x','', '../../secret',[],{},true,[1],[null],[1,2],[1,2,3,4],{roll:'x'}]
for(const [kind,b] of [['v4',base],['v3',legacy]])for(const p of paths)for(let i=0;i<nasty.length;i++){
  change(`${kind}:${p}:${i}`,m=>{const keys=p.split('.');let at=m;for(const k of keys.slice(0,-1))at=at[k]??=(k==='rig'?{}:{});at[keys.at(-1)]=clone(nasty[i])},b)
}
const labels={zh:'生气','zh-Hant':'生氣',en:'Angry',ja:'怒り'}
for(const l of [labels,{ko:'윙크'},{EN:'Shy'},{},undefined])for(const lang of ['ja-JP','zh-TW','zh-Hant-HK','zh-CN','zh','fr-FR','en-GB',''])data.labels.push({labels:l??null,lang,expected:pickLabel(l,lang,'fallback')})
for(const text of ['害羞','にやり','照れ顔','귀엽다','Pouty',''])data.languages.push({text,expected:langOf(text)})
for(const [w,h] of [[64,64],[1024,1536],[832,832],[960,960],[8000,8000]])data.lattice.push({w,h,expected:latticeCell(w,h)})
function zip(entries,count=entries.length){
 let off=0;const parts=[],cd=[]
 for(const e of entries){const name=Buffer.from(e.name),raw=Buffer.from(e.data??'{}'),method=e.method??0,body=method===8?deflateRawSync(raw):raw,h=Buffer.alloc(30),c=Buffer.alloc(46),usize=e.usize??raw.length
 h.writeUInt32LE(0x04034b50);h.writeUInt16LE(method,8);h.writeUInt32LE(body.length,18);h.writeUInt32LE(usize,22);h.writeUInt16LE(name.length,26)
 c.writeUInt32LE(0x02014b50);c.writeUInt16LE(e.flags??0x800,8);c.writeUInt16LE(method,10);c.writeUInt32LE(body.length,20);c.writeUInt32LE(usize,24);c.writeUInt16LE(name.length,28);c.writeUInt32LE(off,42)
 parts.push(h,name,body);cd.push(c,name);off+=h.length+name.length+body.length}
 const end=Buffer.alloc(22);end.writeUInt32LE(0x06054b50);end.writeUInt16LE(count,8);end.writeUInt16LE(count,10);end.writeUInt32LE(Buffer.concat(cd).length,12);end.writeUInt32LE(off,16)
 return Buffer.concat([...parts,...cd,end])
}
async function archive(name,bytes){const blob=new Blob([bytes]),row={name,bytes:bytes.toString('base64'),isZip:await isZip(blob)};try{row.files=Object.fromEntries(await Promise.all([...await unzip(blob)].map(async([k,b])=>[k,Buffer.from(await b.arrayBuffer()).toString('base64')])))}catch(e){row.error=e.code??null}data.zip.push(row)}
await archive('stored',zip([{name:'model.json'},{name:'textures/a.png',data:'png'}]))
await archive('compressed',zip([{name:'Sena/model.json',method:8},{name:'Sena/textures/a.png',data:'png',method:8},{name:'Sena/'},{name:'__MACOSX/Sena/._model.json'},{name:'Sena/.DS_Store'},{name:'Sena\\textures\\b.png',data:'b'}]))
await archive('empty',zip([]));await archive('not-zip',Buffer.from('not a zip'))
for(const name of ['../model.json','/etc/model.json','C:/model.json','a/./model.json'])await archive('path:'+name,zip([{name}]))
for(const [name,change] of [['encrypted',{flags:0x801}],['method',{method:12}],['oversized',{method:8,usize:LIMITS.modelJson+1}],['bad-size',{usize:10}],['bad-inflate-size',{method:8,usize:10}]])await archive(name,zip([{name:'model.json',...change}]))
await archive('duplicate',zip([{name:'a.json'},{name:'a.json'}]));await archive('zip64',zip([{name:'model.json'}],0xffff));await archive('count',zip([{name:'model.json'}],2049))
await archive('bomb',zip([{name:'keyforms.json',data:Buffer.alloc(1024*1024),method:8,usize:1024}]))
const original=zip([{name:'model.json',method:8}]);for(const length of [0,4,30,original.length-1])await archive('truncate:'+length,original.subarray(0,length))
const encoded=gzipSync(Buffer.from(JSON.stringify(data)+'\n'),{level:9})
if(process.argv.includes('--check')){if(!fs.existsSync(dest)||!fs.readFileSync(dest).equals(encoded)){console.error('Format fixture stale');process.exit(1)}console.log('Format fixture current')}else{fs.writeFileSync(dest,encoded);console.log(`Wrote ${data.models.length} model cases, ${data.zip.length} ZIP cases (${encoded.length} bytes)`)}
