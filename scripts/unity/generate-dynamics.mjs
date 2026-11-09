// Execute original numeric methods; stub only Canvas readback and GPU allocation.
import fs from 'node:fs'
import crypto from 'node:crypto'
import zlib from 'node:zlib'
import { fileURLToPath } from 'node:url'
import { Puppet } from '../../src/runtime/puppet.ts'
import { readModel,latticeCell } from '../../src/runtime/format.ts'
const root=fileURLToPath(new URL('../../',import.meta.url))
const gl=new Proxy({getShaderParameter:()=>true,getError:()=>0},{get(t,k){if(k in t)return t[k];return /^[A-Z_0-9]+$/.test(k)?k:()=>({})}})
globalThis.window={devicePixelRatio:1}
let readAlpha=null
globalThis.document={createElement:()=>{const c={width:0,height:0};c.getContext=()=>({drawImage(){},clearRect(){},getImageData(){const data=new Uint8ClampedArray(c.width*c.height*4);if(readAlpha)for(let i=0;i<readAlpha.length;i++)data[4*i+3]=readAlpha[i];return {data}}});return c}}
const copy=x=>JSON.parse(JSON.stringify(x,(_,v)=>ArrayBuffer.isView(v)?Array.from(v):v))
const data={sourceHashes:Object.fromEntries(['src/runtime/puppet.ts','src/runtime/params.ts','src/runtime/format.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),cases:[]}
const stateFields=['params','time','smileShown','mouthFrame','mouthAmp','mouthShape','mouthHeld','mouthW','talkLeft','body','bust','lastHead','drive','chains','chainRuns']
for(const id of fs.readdirSync(root+'characters').sort()){
 const path=root+'characters/'+id+'/model.json';if(!fs.existsSync(path))continue
 const raw=JSON.parse(fs.readFileSync(path)), model=readModel(raw), p=new Puppet({getContext:()=>gl}),im={width:1,height:1}
 readAlpha=null
 p.build(model,model.layers.map(()=>({main:im,frames:[]})),undefined,[])
 const a=model.anchors,h=a.head
 const points=[[-1.5,-1.3,-.2],[0,0,0],[.6,.5,.2],[-.4,1.4,.5],[1.8,2.5,0]].map(([u,v,z])=>[h.cx+u*h.rx,h.cy+v*h.ry,z])
 const r={id,raw,points,layers:[],frames:[]}
 p.layers=model.layers.map((d,index)=>{
  const cell=latticeCell(...model.canvas),cols=Math.ceil((d.x+d.w)/cell)-Math.floor(d.x/cell),rows=Math.ceil((d.y+d.h)/cell)-Math.floor(d.y/cell)
  readAlpha=new Uint8Array(cols*rows);for(const j of [0,Math.floor(readAlpha.length/2),readAlpha.length-1])readAlpha[j]=255
  const L=p.buildLayer(d,im,[],undefined,undefined,undefined,undefined),n=L.rest.length/2
  // Inject the platform-derived samples to test the numeric deformation branches independently of image sampling.
  L.pivY=d.pivot?Float32Array.from({length:n},(_,i)=>d.pivot[1]+i%3-1):undefined;L.pivC=d.pivot?.[1]
  L.outB=new Float32Array(L.rest.length)
  if(index%3===0){L.fol=Float32Array.from({length:n},(_,i)=>i%3*.2);L.folB=Float32Array.from({length:n},(_,i)=>i%2*.7);L.alt=new Float32Array(n*5);for(let i=0;i<n;i++)L.alt.set([L.dep[i]+.02,.8,.2,.4,.5],i*5)}
  L.kf=Array.from({length:3},(_,k)=>Float32Array.from(L.rest,(_,i)=>Math.sin(i+k)*.5))
  r.layers.push({id:d.id,alpha:Buffer.from(readAlpha).toString('base64'),samples:copy({pivY:L.pivY,pivC:L.pivC,fol:L.fol,folB:L.folB,alt:L.alt,kf:L.kf})})
  return L
 })
 p.keys=[{param:'angleX',value:20,scale:1},{param:'angleX',value:-20,scale:1},{param:'angleY',value:25,scale:.8}];p.kfPair=[1,0,-1]
 p.nasal={L:{room:2,sign:1,inner:h.cx-10,w:20},R:{room:2,sign:-1,inner:h.cx+10,w:20}}
 r.keys=p.keys;r.kfPair=p.kfPair;r.nasal=p.nasal
 for(let i=0;i<120;i++){
  const dt=[0,1/120,1/60,1/30,.055,.1,1/144][i%7]
  const input={browLY:Math.sin(i*.4),browRY:Math.cos(i*.3),eyeBallX:Math.sin(i*.6),eyeBallY:Math.cos(i*.8),angleX:45*Math.sin(i*.18),angleY:38*Math.cos(i*.27),angleZ:33*Math.sin(i*.21),bodyX:8*Math.sin(i*.5),bodyZ:-9*Math.cos(i*.3),breath:(1+Math.sin(i*.6))/2,smile:i%19<8?1:0,mouthOpen:[0,.08,.12,.16,.17,.54,.55,.8,.46,.45,.079,0][i%12],vowel:[-1,-.65,-.35,0,.35,.65,1][Math.floor(i/3)%7],eyeLOpen:i%11/10,eyeROpen:1-i%11/10}
  const expr={angry:i%7/6,shy:1-i%9/8},physicsScale=i<40?1:i<80?.3:0
  const tuning=i===20?{turn:.8,nod:.4,hairFront:.6,hairBack:1.3}:null
  const rig=i===30?{pitch_ease:.1,yaw:.3,pitch:.24,roll:.6,bulge_shape:1.4,bulge_extent:[1.2,1.1,1.4,.7,.8],drift:[.05,.15]}:null
  if(i===60){p.resetMouth();p.resetPhysics()}
  Object.assign(p.params,input);p.expr={...expr};p.physicsScale=physicsScale
  if(tuning)Object.assign(p.tuning,tuning);if(rig)Object.assign(p.rig,rig)
  p.kfOff=i>=90&&i<95;p.keyWeights=i>=100&&i<105?[.4,-.2,.8]:undefined
  const poke=i%20===5?[p.chainRuns[0]?.R[0]??h.cx,(p.chainRuns[0]?.R[1]??h.cy)+20,i%40===5?1:-.7]:null
  const hits=poke?p.poke(...poke):0
  p.update(dt,true)
  const placement={view:{x:Math.sin(i*.1)*.25,y:Math.cos(i*.15)*.1,s:.6+(i%9)*.13},move:{x:.05*Math.sin(i),y:.1*Math.sin(i*.4),s:.8+(i%5)*.1},zoom:.7+(i%4)*.4,panY:Math.sin(i*.2)*.15,focusHead:i%2===0,cutBottom:i%3!==0}
  Object.assign(p,placement);p.size=[[512,1024,1515][i%3],[768,640,900][i%3]];p.canvas.clientWidth=[512,340,0][i%3];p.canvas.clientHeight=[768,200,1][i%3];p.render()
  const viewCheck={placement,size:p.size,display:[p.canvas.clientWidth,p.canvas.clientHeight],lastView:p.lastView,points:[[0,0],[13,71],[300,240]].map(pt=>({input:pt,expected:p.canvasToModel(...pt)}))}
  const probes=points.map(([x,y,z])=>{const head=[],body=[],noBust=[];p.headXform(x,y,z,p.params,head);p.bodyOffset(x,y,p.params,body);p.bodyOffset(x,y,p.params,noBust,false);return {head,body,noBust}})
  const visible=model.layers.map(def=>({id:def.id,shown:p.shown({def}),expr:p.exprAlpha(def),mood:p.moodAlpha(def)}))
  const state=Object.fromEntries(stateFields.map(k=>[k,copy(p[k])]))
  // Definitions were already checked by model fixtures; state only here.
  state.chainRuns.forEach(c=>delete c.def)
  r.frames.push({poke,hits,viewCheck,deform:i%15===0||i===119?p.layers.map(L=>({output:Array.from(L.out),outB:Array.from(L.outB),soft:L.soft??0})):null,kfOff:p.kfOff,keyWeights:p.keyWeights??null,dt,input,expr,physicsScale,tuning,rig,reset:i===60,state,probes,visible,ease:p.frameKind.map((_,i)=>p.frameEase(i,p.params.mouthOpen))})
 }
 data.cases.push(r)
}
const output=JSON.stringify(data)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/dynamics.json.gz'
if(process.argv.includes('--check')){if(zlib.gunzipSync(fs.readFileSync(dest)).toString()!==output)throw Error('dynamics fixture differs');console.log('dynamics fixture matches upstream')}
else{fs.writeFileSync(dest,zlib.gzipSync(output,{level:9,mtime:0}));console.log(`dynamics fixture: ${data.cases.length} models, ${data.cases.reduce((s,c)=>s+c.frames.length,0)} frames`)}
