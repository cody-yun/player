// Run original TalkMotion and public tracking/voice drivers; stub device acquisition only.
import fs from 'node:fs';import zlib from 'node:zlib';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {TalkMotion} from '../../src/runtime/talk.ts';import {FaceTracker} from '../../src/runtime/tracking.ts';import {VoiceMeter} from '../../src/runtime/voice.ts';
import {startFaceTracking} from '../../src/tracking.ts';import {startLipSync} from '../../src/voice.ts';import {synthTrace} from '../../src/runtime/trace.ts';import {defaultParams,PARAM_RANGES} from '../../src/runtime/params.ts';
globalThis.document={createElement:()=>({})};
const root=fileURLToPath(new URL('../../',import.meta.url));let seed,calls,now=0;const reset=()=>{seed=72813;calls=0};Math.random=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;calls++;return seed/4294967296};Object.defineProperty(globalThis,'performance',{value:{now:()=>now}});
const data={sourceHashes:Object.fromEntries(['src/tracking.ts','src/voice.ts',...['talk','tracking','voice','trace','params'].map(n=>'src/runtime/'+n+'.ts')].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),talk:[],drivers:[]};
for(const amount of [0,.7,2]){
 reset();const talk=new TalkMotion(),rows=[];talk.amount=amount;
 for(let i=0;i<1800;i++){
  const dt=[1/60,1/120,.05,0,.1][i%5],level=i%400<260?(.5+.5*Math.sin(i*.57)):0,bounce=i%300>120,reset=i===600||i===1300;
  const p={...defaultParams(),eyeBallX:Math.sin(i*.2),eyeBallY:Math.cos(i*.14)};talk.bounce=bounce;if(reset)talk.reset();const offsets={...talk.update(level,dt,p)};
  rows.push({dt,level,bounce,reset,p,expected:{offsets,act:talk.act,hop:talk.hop,pauseBlink:talk.pauseBlink,stats:{...talk.stats},calls}});
 }
 data.talk.push({amount,rows});
}
const trace=synthTrace(true);let tracker,meter,input,voiceLevel,voiceVowel,faceStops,voiceStops;
FaceTracker.prototype.start=async function(){tracker=this;this.ok=true;this.src={next:()=>input};};FaceTracker.prototype.stop=function(){faceStops++;this.ok=false};
VoiceMeter.prototype.start=async function(){meter=this};VoiceMeter.prototype.read=function(){this.level=voiceLevel*this.gain;this.vowel=voiceVowel;return this.level};VoiceMeter.prototype.stop=function(){voiceStops++};
for(const options of [{follow:0,motion:1,bounce:false},{follow:1,motion:2,bounce:true},{follow:4,motion:0,bounce:true},{follow:-1,motion:.3,bounce:false}]){
 reset();faceStops=voiceStops=0;let blinks=0;const drivers=[];const host={blink(){blinks++},use(d){drivers.push(d);drivers.sort((a,b)=>(a.order??0)-(b.order??0));return ()=>{const i=drivers.indexOf(d);if(i>=0){drivers.splice(i,1);d.dispose?.()}}}};
 const face=await startFaceTracking(host,{follow:options.follow,smoothing:.7}),voice=await startLipSync(host,undefined,options),rows=[];
 for(let i=0;i<1600;i++){
  now=i*1000/60;const dt=[1/60,.01,.05,0][i%4];input=i%2===0?trace[Math.floor(i/2)%trace.length]:null;
  tracker.ok=!(i>=920&&i<1020);const ready=tracker.ok;
  if(i===480||i===1100)face.mirror=!face.mirror;if(i===550)face.calibrate();if(i===740)voice.gain=.7;
  if(i===1400){face.stop();face.stop();}if(i===1550){voice.stop();voice.stop();}
  voiceLevel=i%160<95?.5+.4*Math.sin(i*.35):0;voiceVowel=Math.sin(i*.17);
  const params={...defaultParams(),angleX:4,angleY:-2,eyeBallX:.4,eyeBallY:-.3,mouthOpen:.1};const initial={...params};
  const f={params,dt,time:now/1000,tracked:false,move:{x:0,y:0,s:1},add(offsets){for(const [k,v]of Object.entries(offsets)){if(!v)continue;const [lo,hi]=PARAM_RANGES[k];params[k]=Math.max(lo,Math.min(hi,params[k]+v));}}};
  for(const d of drivers)d.update(f,host);
  rows.push({now,dt,input,ready,initial,voiceLevel,voiceVowel,expected:JSON.parse(JSON.stringify({params,move:f.move,tracked:f.tracked,face:face.face,mirror:face.mirror,level:voice.level,gain:voice.gain,blinks,calls,faceStops,voiceStops}))});
 }
 data.drivers.push({options,rows});
}
const output=JSON.stringify(data)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/drivers.json.gz';if(process.argv.includes('--check')){if(zlib.gunzipSync(fs.readFileSync(dest)).toString()!==output)throw Error('driver fixture differs');console.log('driver fixture matches upstream')}else{fs.writeFileSync(dest,zlib.gzipSync(output,{level:9,mtime:0}));console.log('driver fixture: 5400 talk + 6400 integrated frames')}
