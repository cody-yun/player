// ego-browser nodejs; inject absolute root and baseline (true = reproduce old behavior).
// Generated canvas video/audio and injected visibility events. Ego keeps inactive tabs visible.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const task=await taskSpace(9),p=task.page('p1');
const evidence={result:'Running',baseline,visibilityMode:'Injected document.hidden and visibilitychange in real WASM player; not OS tab suspension',states:[]};
const photo=(await fs.readFile(root+'unity/TestResults/mediapipe/portrait.jpg')).toString('base64');
const record=async name=>{
 const value=await p.evaluate(()=>({hidden:document.hidden,player:window.livlivVerification,tracks:window.lifecycle.tracks.map(t=>({kind:t.kind,state:t.readyState})),contexts:window.lifecycle.contexts.map(c=>c.state),pending:window.lifecycle.pending.length}));
 evidence.states.push({name,...value});console.log(name,JSON.stringify(value));return value;
};
const hide=()=>p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:true});document.dispatchEvent(new Event('visibilitychange'));});
const show=()=>p.evaluate(()=>{Object.defineProperty(document,'hidden',{configurable:true,value:false});document.dispatchEvent(new Event('visibilitychange'));});
const released=()=>p.waitForFunction(()=>{const q=window.lifecycle,s=window.livlivVerification;return !s.microphone&&!s.tracking&&q.tracks.every(t=>t.readyState==='ended')&&q.contexts.every(c=>c.state==='closed');},undefined,{timeout:10000});
try{
 await p.goto('http://127.0.0.1:8773/');
 await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:30000});
 await p.evaluate(async photo=>{
  const q=window.lifecycle={tracks:[],contexts:[],pending:[],defer:false,savedMedia:navigator.mediaDevices.getUserMedia,savedAudio:window.AudioContext};
  const picture=new Image();picture.src='data:image/jpeg;base64,'+photo;await picture.decode();
  q.canvas=document.createElement('canvas');q.canvas.width=640;q.canvas.height=480;const ctx=q.canvas.getContext('2d');
  const draw=()=>ctx.drawImage(picture,0,0,820,615,0,0,640,480);draw();q.timer=setInterval(draw,33);
  q.stream=video=>{const stream=video?q.canvas.captureStream(30):q.destination.stream.clone();q.tracks.push(...stream.getTracks());return stream;};
  navigator.mediaDevices.getUserMedia=options=>q.defer?new Promise(resolve=>q.pending.push({resolve,video:!!options.video})):Promise.resolve(q.stream(!!options.video));
  const button=document.createElement('button');button.id='lifecycle-start';button.textContent='Run generated input lifecycle';button.style.cssText='position:fixed;z-index:999;top:0;left:0';
  button.onclick=async()=>{
   if(!q.generator){q.generator=new q.savedAudio();const tone=q.generator.createOscillator(),gain=q.generator.createGain();gain.gain.value=.2;tone.frequency.value=500;q.destination=q.generator.createMediaStreamDestination();tone.connect(gain);gain.connect(q.destination);tone.start();await q.generator.resume();
    window.AudioContext=function(options){const audio=new q.savedAudio(options);q.contexts.push(audio);return audio;};}
   window.livlivUnity.SendMessage('LivLivVerification','StartFace');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');
  };document.body.append(button);
 },photo);
 console.log(await p.snapshot());await p.click('#lifecycle-start',{label:'Starting generated camera and microphone'});
 await p.waitForFunction(()=>window.livlivVerification.faceSeen&&window.livlivVerification.microphone&&window.livlivVerification.mouth>.2,undefined,{timeout:60000});
 await record('active');
 await hide();
 if(baseline){
  const value=await record('hidden-before-fix');
  evidence.result=value.player.tracking&&value.player.microphone&&value.tracks.some(t=>t.state==='live')?'Reproduced':'NotReproduced';
 }else{
  await released();await record('hidden-released');
  const hiddenCount=evidence.states.find(s=>s.name==='hidden-released').tracks.length;
  await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StartFace');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');});
  await released();const refused=await record('hidden-start-refused');if(refused.tracks.length!==hiddenCount)throw Error('Capture started while hidden');
  await show();await released();await record('visible-remains-off');
  await p.click('#lifecycle-start',{label:'Restarting both generated inputs'});
  await p.waitForFunction(()=>window.livlivVerification.faceSeen&&window.livlivVerification.microphone,undefined,{timeout:15000});await record('restarted');
  await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopFace');window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone');});await released();
  await p.evaluate(()=>{window.lifecycle.defer=true;});
  await p.click('#lifecycle-start',{label:'Starting delayed permission requests'});
  await p.waitForFunction(()=>window.lifecycle.pending.length===2,undefined,{timeout:10000});
  await hide();
  // Resolve permission within one JS turn, before Unity pumps Task.Yield again.
  const late=await p.evaluate(async()=>{const q=window.lifecycle;for(const request of q.pending)request.resolve(q.stream(request.video));for(let i=0;i<8;i++)await Promise.resolve();return q.tracks.map(t=>t.readyState);});
  if(late.some(state=>state!=='ended'))throw Error('Late stream survived cancellation before the next Unity frame');
  await released();await record('late-permission-hidden');await show();await released();await record('late-permission-visible');
  await p.evaluate(()=>{window.lifecycle.defer=false;});await p.click('#lifecycle-start',{label:'Restarting after cancelled permission'});
  await p.waitForFunction(()=>window.livlivVerification.faceSeen&&window.livlivVerification.microphone,undefined,{timeout:15000});await record('restart-after-cancel');
  await p.evaluate(()=>window.dispatchEvent(new PageTransitionEvent('pagehide',{persisted:true})));await released();await record('pagehide-released');
  await p.evaluate(()=>window.dispatchEvent(new PageTransitionEvent('pageshow',{persisted:true})));await released();await record('pageshow-remains-off');
  await p.click('#lifecycle-start',{label:'Restarting after page restoration'});
  await p.waitForFunction(()=>window.livlivVerification.faceSeen&&window.livlivVerification.microphone,undefined,{timeout:15000});await record('restart-after-pagehide');
  await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopFace');window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone');});await released();await record('stopped');
  if(evidence.states.some(s=>s.player.scale!==1||s.player.error||s.player.faceError))throw Error('Sample error/scale regression');
  evidence.result='Passed';
 }
 evidence.browser=await p.evaluate(()=>navigator.userAgent);evidence.sourceHashes={};
 for(const name of ['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.Visibility.cs','unity/Assets/Verification/CaptureVisibility.jslib','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLFaceInput.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLVoiceInput.cs'])evidence.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
 const wasm=root+'unity/TestResults/WebGL/Build/WebGL.wasm';evidence.wasmSha256=crypto.createHash('sha256').update(await fs.readFile(wasm)).digest('hex');
 await fs.writeFile(root+'unity/TestResults/webgl-lifecycle'+(baseline?'-before':'')+'.json',JSON.stringify(evidence,null,2));
 console.log({result:evidence.result,states:evidence.states.length});
}finally{
 await show();
 await p.evaluate(async()=>{const q=window.lifecycle;if(!q)return;window.livlivUnity?.SendMessage('LivLivVerification','StopFace');window.livlivUnity?.SendMessage('LivLivVerification','StopMicrophone');navigator.mediaDevices.getUserMedia=q.savedMedia;window.AudioContext=q.savedAudio;clearInterval(q.timer);for(const t of q.tracks)t.stop();for(const c of q.contexts)if(c.state!=='closed')await c.close();if(q.generator)await q.generator.close();document.querySelector('#lifecycle-start')?.remove();});
 await p.evaluate(()=>{delete document.hidden;document.dispatchEvent(new Event('visibilitychange'));window.livlivUnity?.SendMessage('LivLivVerification','SetPose','neutral');});
}
