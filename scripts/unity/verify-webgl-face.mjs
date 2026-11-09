// Run via ego-browser nodejs with absolute root injected. Generated video only; no physical camera permission.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(3)).page('p1');await p.goto('http://127.0.0.1:8770/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:30000});
const evidence={browser:await p.evaluate(()=>navigator.userAgent),input:'Official MediaPipe portrait.jpg rendered into generated canvas MediaStream; no hardware',steps:{}};
const record=async name=>{evidence.steps[name]=await p.evaluate(()=>({player:window.livlivVerification,tracks:(window.faceTestTracks||[]).map(t=>t.readyState),modelRequests:performance.getEntriesByType('resource').filter(r=>/face_landmarker.task|vision_wasm_internal.wasm/.test(r.name)).length}));};
try{
 await p.evaluate(async()=>{
  const box=document.querySelector('#unity-container');box.style.position='static';box.style.transform='none';const out=document.querySelector('#unity-canvas');out.style.width='100%';out.style.height='680px';
  window.faceTestSavedCamera=navigator.mediaDevices.getUserMedia;window.faceTestTracks=[];
  const photo=new Image();photo.src='/portrait.jpg';await photo.decode();
  const canvas=window.faceTestCanvas=document.createElement('canvas');canvas.width=640;canvas.height=480;const c=canvas.getContext('2d');
  window.faceTestAngle=0;window.faceTestBlank=false;window.faceTestDark=false;
  const draw=()=>{c.fillStyle=window.faceTestDark?'black':'#888';c.fillRect(0,0,640,480);if(!window.faceTestBlank){c.save();c.translate(320,240);c.rotate(window.faceTestAngle);c.drawImage(photo,0,0,820,615,-320,-240,640,480);c.restore();}};draw();window.faceTestTimer=setInterval(draw,33);
  window.faceTestStream=()=>{const stream=canvas.captureStream(30);window.faceTestTracks.push(...stream.getTracks());return stream;};
  navigator.mediaDevices.getUserMedia=()=>Promise.resolve(window.faceTestStream());
  window.livlivUnity.SendMessage('LivLivVerification','StartFace');
 });
 await p.waitForFunction(()=>window.livlivVerification.faceSeen,undefined,{timeout:60000});await record('detected');
 await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','CalibrateFace');window.faceTestAngle=.25;});
 await p.waitForFunction(()=>Math.abs(window.livlivVerification.angleZ)>5,undefined,{timeout:15000});await record('rotated');
 await p.screenshot({path:root+'unity/TestResults/webgl-face-turn.png'});
 await p.evaluate(()=>{window.faceTestBlank=true;});await p.waitForFunction(()=>!window.livlivVerification.faceSeen,undefined,{timeout:10000});await record('lost');
 await p.evaluate(()=>{window.faceTestDark=true;});await p.waitForFunction(()=>window.livlivVerification.dark,undefined,{timeout:10000});await record('dark');
 await p.evaluate(()=>{window.faceTestDark=false;window.faceTestBlank=false;window.faceTestAngle=0;});
 await p.waitForFunction(()=>window.livlivVerification.faceSeen&&!window.livlivVerification.dark,undefined,{timeout:10000});await record('reacquired');
 await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StopFace'));
 await p.waitForFunction(()=>!window.livlivVerification.tracking&&window.faceTestTracks.every(t=>t.readyState==='ended'),undefined,{timeout:10000});await record('stopped');
 await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StartFace'));
 await p.waitForFunction(()=>window.livlivVerification.faceSeen,undefined,{timeout:15000});await record('restarted');
 if(evidence.steps.restarted.modelRequests!==evidence.steps.stopped.modelRequests)throw Error('Restart unexpectedly downloaded model again');
 await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopFace');navigator.mediaDevices.getUserMedia=()=>Promise.reject(new DOMException('Test refusal','NotAllowedError'));window.livlivUnity.SendMessage('LivLivVerification','StartFace');});
 await p.waitForFunction(()=>window.livlivVerification.faceStatus.startsWith('Failed:'),undefined,{timeout:10000});await record('denied');
 await p.waitForFunction(f=>window.livlivVerification.frames>f,evidence.steps.denied.player.frames,{timeout:10000});
 await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopFace');window.faceTestPermission=null;navigator.mediaDevices.getUserMedia=()=>new Promise(r=>window.faceTestPermission=r);window.livlivUnity.SendMessage('LivLivVerification','StartFace');});
 await p.waitForFunction(()=>!!window.faceTestPermission,undefined,{timeout:10000});
 await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StopFace'));
 await p.waitForFunction(()=>window.livlivVerification.faceStatus==='Stopped',undefined,{timeout:10000});
 await p.evaluate(()=>window.faceTestPermission(window.faceTestStream()));
 await p.waitForFunction(()=>window.faceTestTracks.every(t=>t.readyState==='ended'),undefined,{timeout:10000});await record('lateCancel');
 evidence.sourceHashes={};for(const path of ['src/runtime/tracking.ts','unity/Packages/ai.livliv.player/Browser~/face-input.mjs','unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivFace.jslib','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLFaceInput.cs','unity/Assets/Verification/PortVerification.cs','unity/Packages/ai.livliv.player/ThirdParty/mediapipe-webgl.json'])evidence.sourceHashes[path]=crypto.createHash('sha256').update(await fs.readFile(root+path)).digest('hex');
 evidence.portraitSha256=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/mediapipe/portrait.jpg')).digest('hex');evidence.date=new Date().toISOString();
 await fs.writeFile(root+'unity/TestResults/webgl-face.json',JSON.stringify(evidence,null,2));console.log(evidence);
}finally{
 await p.evaluate(()=>{if(window.faceTestSavedCamera)navigator.mediaDevices.getUserMedia=window.faceTestSavedCamera;clearInterval(window.faceTestTimer);window.livlivUnity?.SendMessage('LivLivVerification','StopFace');for(const t of window.faceTestTracks||[])t.stop();});
}
