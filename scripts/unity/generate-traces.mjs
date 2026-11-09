// Original trace.ts produces every expected value; browser download/window are captured at the boundary.
import fs from 'node:fs';import zlib from 'node:zlib';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {FaceTracker} from '../../src/runtime/tracking.ts';
import {synthTrace,TraceRecorder,TraceSource,SynthStats} from '../../src/runtime/trace.ts';import {defaultParams} from '../../src/runtime/params.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));let blob,anchor;
globalThis.document={createElement:()=>anchor={click(){}}};URL.createObjectURL=value=>{blob=value;return 'blob:test'};
const data={sourceHashes:Object.fromEntries(['src/runtime/trace.ts','src/runtime/params.ts','src/runtime/tracking.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),synth:[synthTrace(),synthTrace(true)],record:[],stats:[],replay:[],pipeline:[],pipelineRecords:[]};
for(const input of [[],[{t:1000.5,m:[1.23445,-1.23445,0.00005,-0.00005,1.23455,-1.23455],b:[0.33]},{t:1001},{t:1033.5,l:[0.25,0.8]}],data.synth[0].slice(0,40)]){
 const r=new TraceRecorder();input.forEach(f=>r.push(f));r.save();data.record.push({input,expected:JSON.parse(await blob.text())});
}
for(const mirror of [true,false])for(const sparse of [false,true]){
 globalThis.window={};const stats=new SynthStats(()=>mirror),rows=[];
 const times=sparse?[0,23.5,0,23.9,0]:Array.from({length:1441},(_,i)=>(i%720)/30);
 for(const s of times){const p={...defaultParams(),angleX:(mirror?-1:1)*28.6*Math.sin(Math.PI*(s-.08)),angleY:-.125+Math.cos(s)*.2,eyeLOpen:s>=13&&s<13.2?.1:.95,eyeROpen:s>=16&&s<17?.4:.98};stats.observe(s,p);rows.push({s,p:{...p},expected:window.__trackStats?JSON.parse(JSON.stringify(window.__trackStats)):null});p.angleX=999;}
 data.stats.push({mirror,sparse,rows});
}
for(const frames of [[],[{t:0}],[{t:20},{t:50},{t:83}],data.synth[0]]){
 const source=new TraceSource(frames),rows=[];for(const now of [0,0,15,20,50,50,100,110,140,23967,23999,24000,24033,48000,48001,10])rows.push({now,frame:source.next(now),at:source.at(now)});data.replay.push({frames,rows,dur:source.dur});
}
let clock=0;Object.defineProperty(performance,'now',{value:()=>clock});
for(const narrow of [false,true]){
 globalThis.window={};const t=new FaceTracker();t.src=new TraceSource(synthTrace(narrow),true);const stats=new SynthStats(()=>t.mirror),pose=defaultParams(),rec=new TraceRecorder();let active=0,lost=0;
 const next=t.src.next.bind(t.src);t.src.next=now=>{const f=next(now);if(f){rec.push({...f,t:now});if(f.m||f.b)active++;else lost++;}return f};
 for(let i=0;i<=1440;i++){clock=1000+Math.round(i*1000/30);t.poll();t.apply(pose,1/30);stats.observe(t.src.at(clock)/1000,pose);}
 rec.save();const saved=JSON.parse(await blob.text());data.pipelineRecords.push(saved);data.pipeline.push({narrow,count:saved.length,active,lost,fps:t.fps,stats:window.__trackStats,first:saved[0].t,last:saved.at(-1).t});
}
const cap=new TraceRecorder();for(let i=0;i<9010;i++)cap.push({t:500+i*33});cap.save();data.cap={length:cap.frames.length,first:cap.frames[0].t,last:cap.frames.at(-1).t};
const body=JSON.stringify(data)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/traces.json.gz';
if(process.argv.includes('--check')){if(zlib.gunzipSync(fs.readFileSync(dest)).toString()!==body)throw Error('Trace fixture differs');console.log('Trace fixture matches original');}
else{fs.writeFileSync(dest,zlib.gzipSync(body));console.log('1440 synthetic frames, 3 record cases, 4 stats sequences, 4 replay sequences and cap from original');}
