// Capture actual upstream Canvas calls and Mark cache behavior. No duplicate drawing formulas.
import fs from 'node:fs'
import crypto from 'node:crypto'
import { fileURLToPath } from 'node:url'
import * as brand from '../../src/runtime/brand.ts'
import { Mark } from '../../src/mark.ts'
const root=fileURLToPath(new URL('../../',import.meta.url))
globalThis.Path2D=class { constructor(path){this.path=path} }
function canvas() {
  let state={sx:1,sy:1,tx:0,ty:0,globalAlpha:1,shadowColor:null,shadowBlur:0},stack=[],ellipse=null
  const commands=[]
  const methods={
    save(){stack.push({...state})},restore(){state=stack.pop()},
    translate(x,y){state.tx+=x*state.sx;state.ty+=y*state.sy},scale(x,y){state.sx*=x;state.sy*=y},
    beginPath(){ellipse=null},ellipse(x,y,rx,ry){ellipse=[x,y,rx,ry]},
    stroke(p){draw('stroke',p)},fill(p){draw('fill',p)},
    fillText(text,x,y){commands.push({kind:'text',text,x,y,fontSize:Number(state.font.split(' ')[1].slice(0,-2)),textBaseline:state.textBaseline,color:state.fillStyle,shadowColor:state.shadowColor,blur:state.shadowBlur,alpha:state.globalAlpha})},
    clearRect(){commands.length=0},
  }
  function draw(kind,p){commands.push({kind,path:p?.path??null,ellipse:p?null:ellipse,color:kind==='stroke'?state.strokeStyle:state.fillStyle,width:kind==='stroke'?state.lineWidth:0,sx:state.sx,sy:state.sy,tx:state.tx,ty:state.ty,alpha:state.globalAlpha,shadowColor:state.shadowColor,blur:state.shadowBlur})}
  return {commands,g:new Proxy(methods,{get(t,k){return k in t?t[k]:state[k]},set(t,k,v){state[k]=v;return true}})}
}
const data={sourceHashes:Object.fromEntries(['src/runtime/brand.ts','src/mark.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),eyes:[],draws:[],marks:[]}
for(const ms of [-1,0,5114.9,5115,5200,5252.5,5400,5499,5500,11000])data.eyes.push({ms,value:brand.eyeOpen(ms)})
for(const [w,h,px] of [[512,768,1],[768,512,1],[128,256,2],[1024,1536,2]])for(const ms of [0,5252.5])for(const top of [false,true]) {
  for(const type of ['watermark','corner','outline','text']) {
    const c=canvas()
    if(type==='watermark')brand.drawWatermark(c.g,w,h,ms,px,top)
    else if(type==='text')brand.drawMarkText(c.g,w,h,'Creator 이름',px,top)
    else brand.drawCornerLogo(c.g,w,h,ms,px,top,type==='outline')
    data.draws.push({type,w,h,px,ms,top,spot:brand.cornerSpot(w,h,top,px),commands:c.commands})
  }
}
const c=canvas()
globalThis.document={createElement:()=>({width:0,height:0,getContext:()=>c.g})}
const gl=new Proxy({drawingBufferWidth:512,drawingBufferHeight:768},{get(t,k){return k in t?t[k]:()=>({})}})
const mark=new Mark(gl)
for(const [kind,text,center] of [['livliv',null,true],['livliv',null,false],['text','Owner 이름',true],['none',null,true],['text','',true]])for(const ms of [0,1,5115,5200,5252.5,5400,5500]) {
  mark.set(kind,text,center);mark.draw(ms,2)
  data.marks.push({kind,text,center,ms,w:512,h:768,px:2,visible:!!(mark.logo||mark.text),key:mark.drawn,commands:c.commands.slice()})
}
const dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/brand.json'
const output=JSON.stringify(data,null,2)+'\n'
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('brand fixture differs');console.log('brand fixture matches upstream')}
else {fs.writeFileSync(dest,output);console.log(`brand fixture: ${data.draws.length} Canvas cases, ${data.marks.length} Mark transitions`)}
