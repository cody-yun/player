// Source-executed buildLayer fixtures. Canvas pixel readback and WebGL storage only are stubbed.
import fs from 'node:fs'
import crypto from 'node:crypto'
import zlib from 'node:zlib'
import { fileURLToPath } from 'node:url'
import { Puppet } from '../../src/runtime/puppet.ts'
import { readModel,latticeCell } from '../../src/runtime/format.ts'
const root=fileURLToPath(new URL('../../',import.meta.url))
let readAlpha=null
const gl=new Proxy({getShaderParameter:()=>true,getError:()=>0,createBuffer:()=>({}),bindBuffer:(target,b)=>{gl.bound=b},bufferData:(target,data)=>{gl.bound.data=Array.from(data)},},{get(t,k){if(k in t)return t[k];return /^[A-Z_0-9]+$/.test(k)?k:()=>({})}})
globalThis.document={createElement:()=>{const c={width:0,height:0};c.getContext=()=>({drawImage(){},clearRect(){},getImageData(){const d=new Uint8ClampedArray(c.width*c.height*4);if(readAlpha)for(let i=0;i<readAlpha.length&&i<c.width*c.height;i++)d[4*i+3]=readAlpha[i];return {data:d}}});return c}}
const fields=['rest','tri','wHead','wPhys1','wPhys2','dep','wBrow','browSide','browCap','wFront','wJaw','wNeck','wTwist','fol','folB','alt']
const data={sourceHashes:Object.fromEntries(['src/runtime/puppet.ts','src/runtime/format.ts','src/runtime/params.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),models:[],layers:[]}
for(const id of fs.readdirSync(root+'characters').sort()){
 const path=root+'characters/'+id+'/model.json';if(!fs.existsSync(path))continue
 const raw=JSON.parse(fs.readFileSync(path)),model=readModel(raw)
 data.models.push({id,raw})
 const p=new Puppet({getContext:()=>gl})
 const im={width:1,height:1}
 readAlpha=null
 p.build(model,model.layers.map(()=>({main:im,frames:[]})),undefined,[])
 const chosen=model.layers.filter(l=>l.id==='face'||l.group==='body'||l.group==='hair_front'||l.role==='mouth'||l.id==='brows'||l.role==='neck_shadow'||l.bones?.length)
 for(let ix=0;ix<chosen.length;ix++){
  const l=chosen[ix],cell=latticeCell(...model.canvas),gx0=Math.floor(l.x/cell),gy0=Math.floor(l.y/cell),cols=Math.ceil((l.x+l.w)/cell)-gx0,rows=Math.ceil((l.y+l.h)/cell)-gy0
  const alpha=Uint8Array.from({length:cols*rows},(_,i)=>(i*31+ix)%197===0?(i%3===0?1:255):0)
  readAlpha=alpha
  const out=p.buildLayer(l,im,[],undefined,undefined,undefined,undefined)
  const expected=Object.fromEntries(fields.map(f=>[f,out[f]?Array.from(out[f],v=>Number.isFinite(v)?v:String(v)):null]));expected.uv=out.uvs.data
  if(out.skin)expected.skin=Object.fromEntries(Object.entries(out.skin).map(([k,v])=>[k,typeof v==='number'?v:Array.from(v)]))
  else expected.skin=null
  data.layers.push({model:id,id:l.id,alpha:Buffer.from(alpha).toString('base64'),frameKind:p.frameKind,frameJaw:p.frameJaw,expected})
 }
}
// Small malformed-alpha, negative crop, edge occupancy and all-transparent cases are validated separately in C#.
const output=JSON.stringify(data)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/puppet.json.gz'
if(process.argv.includes('--check')){if(zlib.gunzipSync(fs.readFileSync(dest)).toString()!==output)throw Error('puppet fixture differs');console.log('puppet fixture matches upstream')}
else {fs.writeFileSync(dest,zlib.gzipSync(output,{level:9,mtime:0}));console.log(`puppet fixture: ${data.models.length} models, ${data.layers.length} layer cases (${output.length} bytes raw)`)}
