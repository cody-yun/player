import fs from 'node:fs';import zlib from 'node:zlib';import crypto from 'node:crypto';import test from 'node:test';import assert from 'node:assert/strict';
import {createFaceApi,toFrame} from '../../unity/Packages/ai.livliv.player/Browser~/face-input.mjs';
import vm from 'node:vm';
const root=new URL('../../',import.meta.url),data=JSON.parse(zlib.gunzipSync(fs.readFileSync(new URL('unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/face-boundary.json.gz',root))));
const flush=async()=>{for(let i=0;i<20;i++)await Promise.resolve()};
function setup({cameraDelay=false,cameraReject=false,modelDelay=false,playDelay=false}={}){
 const log={stops:0,plays:0,models:0,fetches:0,aborts:0};let resolveCamera;const trackListeners=new Set();
 const track={readyState:'live',stop(){this.readyState='ended';log.stops++},addEventListener(k,fn){assert.equal(k,'ended');trackListeners.add(fn)},removeEventListener(k,fn){assert.equal(k,'ended');trackListeners.delete(fn)}},stream={getTracks:()=>[track]};
 let resolvePlay;const video={readyState:2,currentTime:0,play(){log.plays++;return playDelay?new Promise(r=>resolvePlay=r):Promise.resolve()},pause(){}};
 const state={now:0,cost:0,luma:60,result:{faceLandmarks:[]}};
 const api=createFaceApi({video:()=>video,camera:()=>cameraReject?Promise.reject(Error('denied')):cameraDelay?new Promise(r=>resolveCamera=r):Promise.resolve(stream),
  now:()=>state.now,vision:async()=>({FilesetResolver:{isSimdSupported:async()=>true},FaceLandmarker:{createFromOptions:async(files,options)=>{log.models++;log.files=files;log.options=options;return {detectForVideo(){state.now+=state.cost;return state.result}}}}}),
  fetch:async(url,{signal})=>{log.fetches++;if(modelDelay)return new Promise((resolve,reject)=>signal.addEventListener('abort',()=>{log.aborts++;reject(Error('aborted'))}));return {ok:true,body:{getReader(){let done=false;return {async read(){if(done)return {done:true};done=true;return {done:false,value:new Uint8Array([1,2,3])}}}}}}},
  canvas:()=>({getContext:()=>({drawImage(){},getImageData(){const data=new Uint8ClampedArray(32*24*4);for(let i=0;i<data.length;i+=4)data[i]=data[i+1]=data[i+2]=state.luma;return {data}}})})});
 return {api,log,video,state,resolveCamera:()=>resolveCamera(stream),resolvePlay:()=>resolvePlay(),track,trackListeners,end(){track.readyState='ended';for(const fn of [...trackListeners])fn()}};
}
for(const c of data.cases)test('original polling/result/fps/light '+c.maxFps,async()=>{
 const s=setup(),id=s.api.start({maxFps:c.maxFps});await flush();assert.equal(s.api.state(id).ready,true);
 for(const [i,row] of c.rows.entries()){
  Object.assign(s.state,{now:row.now,cost:row.cost,luma:row.luma,result:row.result});Object.assign(s.video,{currentTime:row.videoTime,readyState:row.readyState});
  const frame=s.api.poll(id,row.now),info=s.api.state(id);assert.deepEqual(JSON.parse(JSON.stringify({frame,fps:info.fps,dark:info.dark})),row.expected,'row '+i);
 }
 assert.equal(s.log.options.runningMode,'VIDEO');assert.equal(s.log.options.baseOptions.delegate,'GPU');s.api.stop(id);s.api.stop(id);assert.equal(s.log.stops,1);
});
test('last cancelled waiter aborts model and late camera stream ends',async()=>{
 const s=setup({modelDelay:true,cameraDelay:true}),id=s.api.start();await flush();s.api.stop(id);s.resolveCamera();await flush();assert.equal(s.track.readyState,'ended');assert.equal(s.log.stops,1);assert.equal(s.log.aborts,2);assert.equal(s.api.state(id).status,-2);
});
test('camera refusal cancels unnecessary model download',async()=>{
 const s=setup({modelDelay:true,cameraReject:true}),id=s.api.start();await flush();assert.equal(s.api.state(id).status,-1);assert.equal(s.api.state(id).ready,false);assert.equal(s.log.aborts,2);
});
test('camera ended event fails and releases input without polling',async()=>{
 const s=setup(),id=s.api.start();await flush();s.end();
 assert.equal(s.api.state(id).status,-1);assert.equal(s.api.state(id).ready,false);assert.match(s.api.state(id).error,/Camera.*ended/);
 assert.equal(s.video.srcObject,null);assert.equal(s.trackListeners.size,0);s.api.stop(id);
});
test('late video play completion cannot revive an ended camera',async()=>{
 const s=setup({playDelay:true}),id=s.api.start();await flush();assert.equal(s.log.plays,1);
 s.end();s.resolvePlay();await flush();assert.equal(s.api.state(id).status,-1);assert.equal(s.api.state(id).ready,false);
 assert.equal(s.video.srcObject,null);assert.equal(s.trackListeners.size,0);s.api.stop(id);
});
test('camera already ended before model readiness never becomes ready',async()=>{
 const s=setup({cameraDelay:true}),id=s.api.start();await flush();s.track.readyState='ended';s.resolveCamera();await flush();
 assert.equal(s.api.state(id).status,-1);assert.equal(s.api.state(id).ready,false);assert.equal(s.trackListeners.size,0);
});
test('another waiting tracker keeps the shared download alive',async()=>{const s=setup({modelDelay:true});const first=s.api.start(),second=s.api.start();await flush();s.api.stop(first);assert.equal(s.log.aborts,0);s.api.stop(second);await flush();assert.equal(s.log.aborts,2);});
test('preload keeps model available after capture stops',async()=>{
 const s=setup();await s.api.preload({});const id=s.api.start();await flush();s.api.stop(id);const second=s.api.start();await flush();assert.equal(s.log.models,1);assert.equal(s.log.fetches,2);s.api.stop(second);
});
test('missing categories/matrix and no landmark result stay optional',()=>{
 assert.deepEqual(toFrame({faceLandmarks:[]},7),{t:7});assert.deepEqual(toFrame({faceLandmarks:[[]]},7),{t:7,m:undefined,b:undefined,l:undefined});
});
test('source fixture hashes',()=>{for(const [p,hash] of Object.entries(data.sourceHashes))assert.equal(crypto.createHash('sha256').update(fs.readFileSync(new URL(p,root))).digest('hex'),hash)});
function bridge(){const ctx={LibraryManager:{library:{}},mergeInto:Object.assign,UTF8ToString:x=>x,HEAPF64:new Float64Array(80),stringToUTF8:s=>ctx.json=s};vm.createContext(ctx);vm.runInContext(fs.readFileSync(new URL('unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivFace.jslib',root),'utf8'),ctx);Object.assign(ctx,ctx.LibraryManager.library);ctx.LivLivFaceBridge=ctx.$LivLivFaceBridge;return ctx;}
test('WASM packed fields use current heap and distinguish lost from no fresh frame',()=>{
 const b=bridge();let frame=null;b.LivLivFaceBridge.entries[1]={inner:1,api:{poll:()=>frame}};
 assert.equal(b.LivLivFace_Poll(1,5,8),0);frame={t:5};assert.equal(b.LivLivFace_Poll(1,5,8),1);
 b.HEAPF64=new Float64Array(100);frame={m:Array.from({length:16},(_,i)=>i),b:Array(18).fill(.7),l:Array(8).fill(.3)};
 assert.equal(b.LivLivFace_Poll(1,6,8),15);assert.deepEqual(Array.from(b.HEAPF64.slice(1,43)),[...frame.m,...frame.b,...frame.l]);
});
test('cancel while module loads never starts camera',async()=>{
 const b=bridge();let resolve,starts=0;b.LivLivFaceBridge.load=()=>new Promise(r=>resolve=r);
 const id=b.LivLivFace_Start('{}',0);b.LivLivFace_Stop(id);resolve({start(){starts++}});await flush();assert.equal(starts,0);assert.equal(b.LivLivFace_State(id),-2);
});
test('preload progress info excludes internal options and API',async()=>{
 const b=bridge();b.LivLivFaceBridge.load=async()=>({preload:()=>new Promise(()=>{}),progress:()=>.6});
 const id=b.LivLivFace_Start(JSON.stringify({moduleUrl:'x'.repeat(10000)}),1);await flush();b.LivLivFace_Info(id,0,4096);const info=JSON.parse(b.json);
 assert.equal(info.progress,.6);assert.equal(info.step,'download');assert.ok(b.json.length<200);b.LivLivFace_Stop(id);
});
