import fs from 'node:fs';import zlib from 'node:zlib';import crypto from 'node:crypto';import {stripTypeScriptTypes} from 'node:module';import {fileURLToPath,pathToFileURL} from 'node:url';
import {synthTrace,BS,LM} from '../../src/runtime/trace.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));let code=stripTypeScriptTypes(fs.readFileSync(root+'src/runtime/tracking.ts','utf8'));
code=code.replace(/from '\.\/(params|filters|trace)'/g,(_,name)=>'from '+JSON.stringify(pathToFileURL(root+'src/runtime/'+name+'.ts').href));code+='\nexport {toFrame};';
const {FaceTracker,toFrame}=await import('data:text/javascript;base64,'+Buffer.from(code).toString('base64'));
let now=0,luma=0;Object.defineProperty(performance,'now',{value:()=>now});globalThis.document={createElement:()=>({readyState:0,currentTime:0,getContext:()=>({drawImage(){},getImageData(){const data=new Uint8ClampedArray(32*24*4);for(let i=0;i<data.length;i+=4)data[i]=data[i+1]=data[i+2]=luma;return {data}}})})};
function result(f){if(!f.m&&!f.b)return {faceLandmarks:[]};const l=Array.from({length:478},()=>({x:0,y:0,z:0}));LM.forEach((id,i)=>{l[id]={x:f.l?.[2*i]??0,y:f.l?.[2*i+1]??0,z:0}});return {faceLandmarks:[l],faceBlendshapes:f.b?[{categories:BS.map((categoryName,i)=>({categoryName,score:f.b[i]}))}]:[],facialTransformationMatrixes:f.m?[{data:f.m}]:[]};}
const trace=synthTrace(),cases=[];
for(const maxFps of ['auto',10,0]){
 const t=new FaceTracker(),rows=[];t.maxFps=maxFps;let current,cost,called;
 t.lm={detectForVideo(){called=true;now+=cost;return current}};
 for(let i=0;i<240;i++){
  now=i*16;const before=now;cost=[1,10,80,300][i%4];called=false;luma=[10,45,60,38][Math.floor(i/60)];if(i%5)t.video.currentTime=Math.floor(i/2)/30;t.video.readyState=i%7?2:1;
  const f=i%17===0?{t:i}:trace[(i*3)%trace.length];current=result(f);t.poll();rows.push({now:before,cost,luma,videoTime:t.video.currentTime,readyState:t.video.readyState,result:current,expected:{frame:called?toFrame(current,before):null,fps:t.fps,dark:t.dark}});
 }
 cases.push({maxFps,rows});
}
const sourceHashes={};for(const p of ['src/runtime/tracking.ts','src/runtime/trace.ts'])sourceHashes[p]=crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex');
const text=JSON.stringify({sourceHashes,cases})+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/face-boundary.json.gz';if(process.argv.includes('--check')){if(zlib.gunzipSync(fs.readFileSync(dest)).toString()!==text)throw Error('face boundary fixture differs');console.log('face boundary fixture matches upstream')}else{fs.writeFileSync(dest,zlib.gzipSync(text));console.log('face boundary fixture: 720 original polls')}
