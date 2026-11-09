// Execute unmodified upstream PlayerBase.start/setPoster/draw/stop with a CSS-only surface double.
import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {PlayerBase} from '../../src/base.ts';import {defaultParams} from '../../src/runtime/params.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));
globalThis.cancelAnimationFrame=()=>{};globalThis.addEventListener=()=>{};globalThis.removeEventListener=()=>{};globalThis.document={addEventListener(){},removeEventListener(){}};
class Host extends PlayerBase{
 constructor(){const css={'background-image':'original','background-position':'left','background-size':'auto','background-repeat':'repeat'};super({addEventListener(){},removeEventListener(){},style:{getPropertyValue(k){return css[k]??''},setProperty(k,v){css[k]=v}}},defaultParams(),{autoplay:false,idle:false});this.css=css;this.shown={};this.draws=0;}
 get drawable(){return !!this.info}get clock(){return 0}get shownExpr(){return this.shown}seen(){return {}}get gl(){return null}show(){this.draws++}free(){}
}
const scenarios=[
 {name:'first frame',steps:[['start','a','cover.webp'],['resolve','a'],['draw'],['dispose']]},
 {name:'failure restores background',steps:[['start','a','a.png'],['reject','a']]},
 {name:'newer poster survives old failure',steps:[['start','a','a.png'],['start','b','b.png'],['reject','a'],['resolve','b'],['draw']]},
 {name:'new load without poster',steps:[['start','a','a.png'],['start','b',null],['reject','a'],['reject','b']]},
 {name:'precancel preserves current',steps:[['start','a','a.png'],['precancel','b','b.png'],['resolve','a'],['draw']]},
 {name:'loaded character stays',steps:[['start','a','a.png'],['resolve','a'],['draw'],['start','b','b.png'],['reject','b']]},
 {name:'cancel and dispose',steps:[['start','a','a.png'],['cancel','a'],['reject','a'],['start','b','b.png'],['dispose'],['reject','b']]},
 {name:'old success cannot clear newer poster',steps:[['start','a','a.png'],['start','b','b.png'],['resolve','a',false],['reject','b']]}
];
for(const row of scenarios){const h=new Host(),jobs={},expected=[];
 for(const [op,id,poster] of row.steps){
  if(op==='start'||op==='precancel'){const controller=new AbortController();if(op==='precancel')controller.abort();const job={controller};jobs[id]=job;job.task=h.start({poster,signal:controller.signal},()=>new Promise((resolve,reject)=>{job.resolve=resolve;job.reject=reject}));job.task.catch(()=>{});if(op==='precancel')await job.task.catch(()=>{});}
  else if(op==='resolve'){if(poster!==false)h.info={expressions:[]};jobs[id].resolve();await jobs[id].task;}
  else if(op==='reject'){jobs[id].reject(new Error('failed'));await jobs[id].task.catch(()=>{});}
  else if(op==='cancel')jobs[id].controller.abort();else if(op==='draw')h.resize();else h.dispose();
  const css=h.css;const shown=css['background-image'];expected.push({poster:shown==='original'?null:JSON.parse(shown.slice(4,-1)),background:css['background-position']+' '+css['background-size']+' '+css['background-repeat'],draws:h.draws});
 }
 row.expected=expected;h.dispose();
}
const data={sourceHashes:{'src/base.ts':crypto.createHash('sha256').update(fs.readFileSync(root+'src/base.ts')).digest('hex')},scenarios};
const output=JSON.stringify(data,null,2)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/poster.json';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('poster fixture differs');console.log('poster fixture matches upstream')}else{fs.writeFileSync(dest,output);console.log('poster scenarios: '+scenarios.length)}
