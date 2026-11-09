// Execute original private save/restore/read methods; replace only localStorage and wall clock.
import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {FaceTracker} from '../../src/runtime/tracking.ts';import {synthTrace} from '../../src/runtime/trace.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));globalThis.document={createElement:()=>({})};Date.now=()=>1794210000123;
const valid={x:12,y:-8,z:3,bl:.2,br:.4,px:6,py:2,pz:-43,at:0};
const bad=['','null','false','0','[]','"value"','{','{"x":NaN}',JSON.stringify({...valid,x:'12'}),JSON.stringify({...valid,y:null}),JSON.stringify({...valid,z:true}),JSON.stringify({...valid,bl:null}),'{"x":1e400,"y":0,"z":0,"bl":0,"br":0}'];
const data={sourceHashes:Object.fromEntries(['src/runtime/tracking.ts','src/runtime/trace.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),cases:[]};
function scenario(name,{stored=null,mirror=true,id='주인:character',getFails=false,setFails=false,steps=[]}={}){
 let value=stored,reads=0,writes=[];const key='lm_calib:'+id;
 globalThis.localStorage={getItem(k){if(k!==key)throw Error('Unexpected key');reads++;if(getFails)throw Error('denied');return value},setItem(k,v){if(k!==key)throw Error('Unexpected key');writes.push(JSON.parse(v));if(setFails)throw Error('quota');value=v}};
 const t=new FaceTracker();t.calibId=id;t.setMirror(mirror);const rows=[];
 for(const step of [{op:'restore'},...steps]){
  if(step.op==='restore')t.restore();else if(step.op==='frame')t.read(step.frame,step.now);else if(step.op==='mirror')t.setMirror(step.value);else if(step.op==='calibrate')t.calibrate();
  rows.push({step,expected:JSON.parse(JSON.stringify({zero:t.zero,calibrated:t.calibrated,pos:t.pos,target:t.target,mirror:t.mirror,reads,writes}))});
 }
 data.cases.push({name,stored,mirror,id,getFails,setFails,rows});
}
// No post-read storage parsing for deliberately invalid JSON: expected persisted bytes are unnecessary.
for(const mirror of [true,false]){
 for(const stored of [null,...bad,JSON.stringify(valid),JSON.stringify({...valid,bl:-2,br:4}),JSON.stringify({x:12,y:-8,z:3,bl:.2,br:.4}),JSON.stringify({...valid,px:null}),JSON.stringify({...valid,py:'2'})])scenario('restore '+mirror+' '+String(stored),{stored,mirror});
 const frames=synthTrace(true).slice(0,32).map((frame,i)=>({op:'frame',frame,now:frame.t}));
 for(const stored of [null,JSON.stringify(valid),JSON.stringify({x:12,y:-8,z:3,bl:.2,br:.4})])scenario('auto/manual/restart '+mirror+' '+(stored?'saved':'fresh'),{stored,mirror,steps:[{op:'calibrate'},...frames,{op:'mirror',value:!mirror},{op:'calibrate'},{op:'restore'}]});
 scenario('denied read '+mirror,{mirror,getFails:true,steps:frames});
 scenario('quota write '+mirror,{mirror,setFails:true,steps:frames});
 scenario('remember disabled '+mirror,{mirror,id:'',steps:frames});
}
const dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/calibration.json';const body=JSON.stringify(data)+'\n';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==body)throw Error('Calibration fixture differs');console.log('Calibration fixture matches original');}
else{fs.writeFileSync(dest,body);console.log(data.cases.length+' original calibration scenarios');}
