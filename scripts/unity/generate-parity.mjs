// Execute upstream code, not a second implementation. Requires Node >=22.18 and test/ts.mjs.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import { fileURLToPath } from 'node:url'
import { defaultParams, PARAM_RANGES, smooth } from '../../src/runtime/params.ts'
import { OneEuro } from '../../src/runtime/filters.ts'
import { ActionPlayer } from '../../src/runtime/actions.ts'
import { VowelCue, vowelTrack } from '../../src/runtime/vowel.ts'
import { VoiceMeter } from '../../src/runtime/voice.ts'
import { FaceTracker } from '../../src/runtime/tracking.ts'
import { synthTrace, TraceSource, BS, LM } from '../../src/runtime/trace.ts'

const root = fileURLToPath(new URL('../../', import.meta.url))
const dest = path.join(root, 'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/parity.json')
const sourcePaths = ['params', 'filters', 'actions', 'vowel', 'voice', 'tracking', 'trace'].map(n => `src/runtime/${n}.ts`)
const sourceHashes = Object.fromEntries(sourcePaths.map(p => [p, crypto.createHash('sha256').update(fs.readFileSync(path.join(root, p))).digest('hex')]))
const data = { sourceHashes, defaults: defaultParams(), ranges: PARAM_RANGES, smooth: [], filters: [], actions: [], vowels: [], tracks: [], voice: [], tracking: [], trace: [], BS, LM }
for (const [a,b] of [[0,1],[.95,.7],[-30,30]]) for (const x of [-31,-.1,0,.1,.5,.8,1,30,31]) data.smooth.push({a,b,x,expected:smooth(a,b,x)})
const euro = new OneEuro(.5,.05)
for (let i=0;i<160;i++) {
  const dt = i===60 ? 0 : i===80 ? -.1 : i===90 ? .5 : [1/60,1/30,.01][i%3], v = Math.sin(i*.18)*20
  const reset = i===110; if (reset) euro.reset()
  data.filters.push({v,dt,reset,expected:euro.filter(v,dt)})
}
for (const id of ['nod','shake','tilt','hop','shy','away']) {
  const a = new ActionPlayer(), rows=[]; a.play(id)
  for(let i=0;i<200;i++) {
    const play = i===12 ? id : null; if(play) a.play(play)
    const setAway = i===70 ? true : i===120 ? false : null; if(setAway!==null) a.setAway(setAway,true)
    const dt=[1/60,1/30,.01][i%3]
    rows.push({dt,play,setAway,offsets:{...a.update(dt)},y:a.y,awayW:a.awayW,busy:a.busy,away:a.away,auto:a.auto,pose:a.awayPose(i/60)})
  }
  data.actions.push({id,rows})
}
const cue = new VowelCue()
for(let i=0;i<100;i++) {
  const power=Float32Array.from({length:50},(_,k)=> 1e-7+ ((k>=3&&k<9)? (i<40 ? .1 : .001) : (k>=18&&k<35)?(i<40 ? .0001 : .02): .00001))
  const level=i%11===0?.1:.8,dt=1/60,reset=i===50
  if(reset)cue.reset()
  data.vowels.push({power:[...power],level,dt,hz:100,reset,expected:cue.push(power,100,level,dt),center:cue.center})
}
for(const sr of [16000,22050,44100,48000]) {
  const x=Float32Array.from({length:Math.round(sr*.1)},(_,i)=>.15*Math.sin(2*Math.PI*350*i/sr)+.08*Math.sin(2*Math.PI*2100*i/sr))
  const env=Float32Array.from({length:10},(_,i)=>i<2?.1:.8),lead=2
  data.tracks.push({sr,x:[...x],env:[...env],fps:60,lead,expected:[...vowelTrack(x,sr,env,60,lead)]})
}
// DOM/device stubs only at browser boundaries; all mappings run upstream unmodified.
globalThis.document={createElement:()=>({readyState:0,getContext:()=>null})}
let now=1000
Object.defineProperty(performance,'now',{configurable:true,value:()=>now})
const meter = new VoiceMeter()
let sample=0
meter.an={getFloatTimeDomainData:buf=>buf.fill(sample)}
for(let i=0;i<160;i++) {
  const dt=i===0?1/60:[1/60,1/30,.2][i%3]; if(i>0)now+=dt*1000
  sample=Math.fround(i<20||i>120?.001:Math.abs(Math.sin(i*.31))*.12)
  meter.gain=i>90?.6:1
  data.voice.push({sample,dt:Math.min(.1,dt),gain:meter.gain,expected:meter.read()})
}
for(const config of [
  {narrow:false,mirror:true,smoothing:.5,sync:'turn'},
  {narrow:true,mirror:false,smoothing:0,sync:'always'},
  {narrow:false,mirror:false,smoothing:1,sync:'off'},
]) {
  const tracker=new FaceTracker(); tracker.setMirror(config.mirror); tracker.setSmoothing(config.smoothing);tracker.blinkSync=config.sync
  let input; tracker.src={next:()=>input}
  const frames=synthTrace(config.narrow),rows=[],pose=defaultParams()
  for(let i=0;i<frames.length;i++) {
    input=frames[i];now=input.t+1000
    const toggle=i===630, calibrate=i===60
    if(toggle)tracker.setMirror(!tracker.mirror)
    if(calibrate)tracker.calibrate()
    const ok=tracker.poll(),dt=1/30
    if(ok)tracker.apply(pose,dt)
    rows.push({frame:input,now,toggle,calibrate,dt,ok,target:{...tracker.target},params:{...pose},pos:{...tracker.pos},zero:{...tracker.zero},calibrated:tracker.calibrated,lostFor:tracker.lostFor})
  }
  data.tracking.push({config,rows})
}
const frames=[{t:0},{t:33,b:[.1]},{t:66,b:[.2]}], source=new TraceSource(frames)
for(const now of [1000,1001,1033,1066,1090,1099,1100,1133,1198]) data.trace.push({now,at:source.at(now),expected:source.next(now)})
const output=JSON.stringify(data)+'\n'
if(process.argv.includes('--check')) {
  if(!fs.existsSync(dest)||fs.readFileSync(dest,'utf8')!==output) {console.error('Parity fixture is stale. Review upstream changes, then regenerate.');process.exit(1)}
  console.log('Parity fixture matches the current TypeScript source.')
} else {fs.writeFileSync(dest,output);console.log(`Wrote ${path.relative(root,dest)} (${output.length} bytes)`)}
