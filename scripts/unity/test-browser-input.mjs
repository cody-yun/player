import fs from 'node:fs';import vm from 'node:vm';import test from 'node:test';import assert from 'node:assert/strict';
const plugin=fs.readFileSync(new URL('../../unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivBrowser.jslib',import.meta.url),'utf8');
function setup({reject,delayed=false,suspended=false,failNode=false}={}){
 const log={stops:0,closes:0,resumes:0,disconnects:0,nodes:[],listeners:new Map()},trackListeners=new Set();
 const track={readyState:'live',stop(){this.readyState='ended';log.stops++},addEventListener(k,fn){assert.equal(k,'ended');trackListeners.add(fn)},removeEventListener(k,fn){assert.equal(k,'ended');trackListeners.delete(fn)}};
 const stream={getTracks:()=>[track]};let resolveCapture;
 const capture=new Promise((resolve,rejectCapture)=>{resolveCapture=resolve;if(reject)rejectCapture(reject);else if(!delayed)resolve(stream)});
 class Node{constructor(kind){this.kind=kind;this.connections=[];this.frequency={value:0};this.Q={value:0};log.nodes.push(this)}connect(node){this.connections.push(node)}disconnect(){log.disconnects++}getFloatTimeDomainData(a){a.fill(.125)}getFloatFrequencyData(a){a.fill(-30)}}
 class AudioContext {constructor(){this.state=suspended?'suspended':'running';this.sampleRate=48000;log.ctx=this}createBiquadFilter(){if(failNode)throw Error('node failed');return new Node('band')}createAnalyser(){return new Node('an')}createMediaStreamSource(s){assert.equal(s,stream);return new Node('source')}resume(){log.resumes++;return Promise.resolve()}close(){log.closes++;this.state='closed';return Promise.resolve()}}
 const window={AudioContext,addEventListener(k,fn){log.listeners.set(k,fn)},removeEventListener(k,fn){assert.equal(log.listeners.get(k),fn);log.listeners.delete(k)}};
 const context=vm.createContext({console,Float32Array,Promise,Error,Math,String,window,navigator:{mediaDevices:{getUserMedia(options){log.options=options;return capture}}},performance:{now:()=>1234},HEAPF32:new Float32Array(4096),LibraryManager:{library:{}},mergeInto:(a,b)=>Object.assign(a,b),stringToUTF8:(str,p,n)=>{log.error=str}});
 vm.runInContext(plugin,context);const lib=context.LibraryManager.library;context.LivLivBrowser=lib.$LivLivBrowser;
 return {context,lib,log,stream,track,trackListeners,end(){track.readyState='ended';for(const fn of [...trackListeners])fn()},resolve:()=>resolveCapture(stream)};
}
const flush=async()=>{await Promise.resolve();await Promise.resolve();};
test('source graph, coefficients, float heap and memory growth',async()=>{
 const {context,lib,log}=setup();const id=lib.LivLivVoice_Start();assert.equal(lib.LivLivVoice_State(id),0);await flush();assert.equal(lib.LivLivVoice_State(id),1);
 assert.equal(log.options.audio.echoCancellation,true);const [band,an,spec,src]=log.nodes;assert.equal(band.type,'bandpass');assert.equal(band.frequency.value,650);assert.equal(band.Q.value,.55);assert.equal(an.fftSize,1024);assert.equal(spec.smoothingTimeConstant,0);assert.deepEqual(src.connections,[band,spec]);assert.deepEqual(band.connections,[an]);
 assert.equal(lib.LivLivVoice_Read(id,0,4096),48000);assert.equal(context.HEAPF32[0],.125);assert.equal(context.HEAPF32[1024],Math.fround(.001));
 context.HEAPF32=new Float32Array(8192);lib.LivLivVoice_Read(id,0,4096);assert.equal(context.HEAPF32[0],.125);assert.equal(lib.LivLivVoice_Now(),1234);
 lib.LivLivVoice_Stop(id);lib.LivLivVoice_Stop(id);assert.equal(log.stops,1);assert.equal(log.closes,1);assert.equal(log.listeners.size,0);assert.equal(lib.LivLivVoice_State(id),-2);assert.equal(lib.LivLivVoice_Read(id,0,4096),0);
});
test('cancel pending permission stops the late stream without creating a context',async()=>{
 const s=setup({delayed:true});const id=s.lib.LivLivVoice_Start();s.lib.LivLivVoice_Stop(id);s.resolve();await flush();assert.equal(s.log.stops,1);assert.equal(s.log.ctx,undefined);assert.equal(s.lib.LivLivVoice_State(id),-2);
});
test('permission denial remains readable until disposal',async()=>{
 const s=setup({reject:Object.assign(Error('denied'),{name:'NotAllowedError'})});const id=s.lib.LivLivVoice_Start();await flush();assert.equal(s.lib.LivLivVoice_State(id),-1);s.lib.LivLivVoice_Error(id,0,1024);assert.equal(s.log.error,'NotAllowedError: denied');s.lib.LivLivVoice_Stop(id);assert.equal(s.log.stops,0);
});
test('node construction failure releases an acquired capture',async()=>{
 const s=setup({failNode:true});const id=s.lib.LivLivVoice_Start();await flush();assert.equal(s.lib.LivLivVoice_State(id),-1);assert.equal(s.log.stops,1);assert.equal(s.log.closes,1);s.lib.LivLivVoice_Stop(id);assert.equal(s.log.stops,1);
});
test('suspended context waits for gesture and listeners leave with capture',async()=>{
 const s=setup({suspended:true});const id=s.lib.LivLivVoice_Start();await flush();assert.equal(s.lib.LivLivVoice_State(id),2);assert.equal(s.lib.LivLivVoice_Read(id,0,4096),0);s.log.listeners.get('pointerdown')();assert.equal(s.log.resumes,2);s.log.ctx.state='running';assert.equal(s.lib.LivLivVoice_State(id),1);s.lib.LivLivVoice_Stop(id);assert.equal(s.log.listeners.size,0);
});
test('closed context retires its stream',async()=>{
 const s=setup();const id=s.lib.LivLivVoice_Start();await flush();s.log.ctx.state='closed';assert.equal(s.lib.LivLivVoice_State(id),-1);assert.equal(s.log.stops,1);s.lib.LivLivVoice_Stop(id);assert.equal(s.log.stops,1);
});
test('device ended event releases audio without a Unity poll',async()=>{
 const s=setup();const id=s.lib.LivLivVoice_Start();await flush();s.end();
 assert.equal(s.log.closes,1);assert.equal(s.trackListeners.size,0);assert.equal(s.log.listeners.size,0);
 assert.equal(s.lib.LivLivVoice_State(id),-1);s.lib.LivLivVoice_Error(id,0,1024);assert.match(s.log.error,/Microphone.*ended/);
 s.lib.LivLivVoice_Stop(id);assert.equal(s.log.closes,1);
});
test('a stream already ended when permission resolves is rejected without an audio graph',async()=>{
 const s=setup({delayed:true});const id=s.lib.LivLivVoice_Start();s.track.readyState='ended';s.resolve();await flush();
 assert.equal(s.lib.LivLivVoice_State(id),-1);assert.equal(s.log.ctx,undefined);assert.equal(s.trackListeners.size,0);
});
