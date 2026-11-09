// Original idle/follow methods, seeded Math.random only. No copied motion formulas.
import fs from 'node:fs';import zlib from 'node:zlib';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {LivelyIdle,IDLE_MOVES} from '../../src/runtime/idle.ts';import {IdleDriver} from '../../src/runtime/puppet.ts';import {Idle} from '../../src/follow.ts';import {defaultParams} from '../../src/runtime/params.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));
const seen={model:{anchors:{head:{cx:300,cy:220,rx:100,ry:140}}},lastView:[.003,.003,-1,-1.15,768,1024]};
const data={sourceHashes:Object.fromEntries(['src/runtime/idle.ts','src/runtime/puppet.ts','src/runtime/params.ts','src/follow.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),moves:IDLE_MOVES,seen,cases:[]};
const clone=x=>JSON.parse(JSON.stringify(x));const originalRandom=Math.random;
for(const kind of ['classic','attend','turn','reduced','follow']){
 let state=123456789,calls=0;Math.random=()=>{state=(Math.imul(state,1664525)+1013904223)>>>0;calls++;return state/4294967296};
 const wrapper=kind==='follow'?new Idle():null,idler=wrapper?wrapper.lv:kind==='classic'?new IdleDriver():new LivelyIdle();
 if(kind==='turn')Object.assign(idler.style,{pointer:'turn',bodyTurn:.35,bodyTilt:.12,look:.7,wander:.6,moves:.8});if(kind==='reduced')idler.moves=0;
 const p=defaultParams(),row={kind,frames:[]};let time=0;
 for(let i=0;i<1800;i++){
  const dt=[1/60,1/120,.03,.05,-.01,0,.15][i%7];time+=Math.max(0,dt);
  const busy=i%160>130,tracked=i>=800&&i<950,faceFree=i%250<210;
  const look={x:.7*Math.sin(i*.04),y:.6*Math.cos(i*.025)};
  const pointer=i<200?{x:.65*Math.sin(i*.02),y:.3,d:2,on:true}:i<400?{x:.1,y:.05,d:.3,on:true}:i<600?{x:-.9,y:.6,d:3,on:true,keep:true}:{x:0,y:0,d:99,on:false};
  const forced=kind!=='reduced'&&kind!=='classic'&&kind!=='follow'&&[120,420,720,1020,1320].includes(i)?{id:Object.keys(IDLE_MOVES)[[120,420,720,1020,1320].indexOf(i)],t:0,side:i%240===0?-1:1,fade:1}:null;
  let event=null;
  if(wrapper){
   wrapper.shown(p);
   event=i===200?{kind:'look',x:.7,y:.6}:i===300?{kind:'leave'}:i===400?{kind:'look',x:null,y:0}:i===500?{kind:'blink'}:{kind:'pointer',x:384+500*Math.sin(i*.02),y:250+300*Math.cos(i*.015),on:i%600<550};
   if(event.kind==='look')wrapper.lookAt(seen,event.x,event.y,768,1024);else if(event.kind==='leave')wrapper.leave();else if(event.kind==='blink')wrapper.blink(time);else wrapper.pointer(seen,event.x,event.y,768,1024,event.on);
   p.smile=i%240<40?.8:.15;p.mouthOpen=.2;wrapper.apply(p,time,dt,{busy,tracked,faceFree});
  }else{
   idler.look=look;if(kind!=='classic'){idler.busy=busy;idler.tracked=tracked;idler.faceFree=faceFree;idler.shown={x:p.angleX+2,z:p.angleZ-1};idler.pointer=pointer;if(forced)idler.move={...forced};}
   idler.apply(p,time,dt);
  }
  row.frames.push({dt,time,busy,tracked,faceFree,look,pointer,forced,event,expected:clone(p),calls,state:clone({nextBlink:idler.nextBlink,blinkT:idler.blinkT,nextSaccade:idler.nextSaccade,gaze:idler.gaze,...kind==='classic'?{}:{nextMove:idler.nextMove,move:idler.move,last:idler.last,pointer:idler.pointer}})});
 }
 data.cases.push(row);
}
Math.random=originalRandom;
const output=JSON.stringify(data)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/idle.json.gz';
if(process.argv.includes('--check')){if(zlib.gunzipSync(fs.readFileSync(dest)).toString()!==output)throw Error('idle fixture differs');console.log('idle fixture matches upstream')}
else{fs.writeFileSync(dest,zlib.gzipSync(output,{level:9,mtime:0}));console.log('idle fixture: 5 sequences, 9000 frames')}
