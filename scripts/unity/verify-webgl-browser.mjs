// Execute with ego-browser nodejs and an injected absolute root. Uses generated audio, never hardware.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(3)).page('p1');await p.goto('http://127.0.0.1:8770/');
await p.waitForFunction(()=>window.livlivVerification&&window.livlivVerification.frames>15,undefined,{timeout:30000});
const evidence={browser:await p.evaluate(()=>navigator.userAgent),initial:await p.evaluate(()=>window.livlivVerification),poses:{}};
if(evidence.initial.error)throw Error(evidence.initial.error);
await p.evaluate(()=>{const box=document.querySelector('#unity-container');box.style.position='static';box.style.transform='none';const canvas=document.querySelector('#unity-canvas');canvas.style.width='100%';canvas.style.height='680px';});
for(const pose of ['neutral','turn','blink','talk']){
 await p.evaluate(pose=>window.livlivUnity.SendMessage('LivLivVerification','SetPose',pose),pose);
 await p.waitForFunction(pose=>{const s=window.livlivVerification;return s.angleX===(pose==='turn'?8:0)&&s.angleY===(pose==='turn'?-3:0);},pose,{timeout:10000});
 await p.evaluate(()=>{window.livlivFrame=null;window.livlivUnity.SendMessage('LivLivVerification','CaptureFrame');});
 await p.waitForFunction(()=>!!window.livlivFrame,undefined,{timeout:10000});
 const result=await p.evaluate(()=>({image:window.livlivFrame,state:window.livlivVerification}));
 const name='webgl-char06-'+pose+'.png';await fs.writeFile(root+'unity/TestResults/'+name,Buffer.from(result.image,'base64'));evidence.poses[pose]={file:name,state:result.state};
}
await p.evaluate(()=>{
 window.savedGetUserMedia=navigator.mediaDevices.getUserMedia;window.savedAudioContext=window.AudioContext;
 window.generatedTracks=[];window.captureContexts=[];
 const button=document.createElement('button');button.id='generated-audio-test';button.textContent='Run generated audio test';
 button.onclick=async()=>{
  const ctx=window.testAudioContext=new window.savedAudioContext();const osc=ctx.createOscillator();const gain=ctx.createGain();const dest=window.testAudioDestination=ctx.createMediaStreamDestination();
  osc.frequency.value=500;gain.gain.value=.2;osc.connect(gain);gain.connect(dest);osc.start();await ctx.resume();
  window.AudioContext=function(options){const capture=new window.savedAudioContext(options);window.captureContexts.push(capture);return capture;};
  navigator.mediaDevices.getUserMedia=()=>{const stream=dest.stream.clone();window.generatedTracks.push(...stream.getTracks());return Promise.resolve(stream);};
  window.livlivUnity.SendMessage('LivLivVerification','SetPose','neutral');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');
 };
 document.body.prepend(button);
});
console.log(await p.snapshot());
await p.click('#generated-audio-test',{label:'Testing generated audio capture'});
await p.waitForFunction(()=>window.livlivVerification.microphone&&window.livlivVerification.mouth>.2,undefined,{timeout:15000});
evidence.audio=await p.evaluate(()=>({player:window.livlivVerification,tracks:window.generatedTracks.map(t=>t.readyState),contexts:window.captureContexts.map(c=>c.state)}));
await p.screenshot({path:root+'unity/TestResults/webgl-generated-audio.png'});
await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone'));
await p.waitForFunction(()=>!window.livlivVerification.microphone&&window.generatedTracks.every(t=>t.readyState==='ended')&&window.captureContexts.every(c=>c.state==='closed'),undefined,{timeout:10000});
evidence.stop=await p.evaluate(()=>({player:window.livlivVerification,tracks:window.generatedTracks.map(t=>t.readyState),contexts:window.captureContexts.map(c=>c.state)}));
await p.evaluate(()=>{navigator.mediaDevices.getUserMedia=()=>Promise.reject(new DOMException('Test permission denial','NotAllowedError'));window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');});
await p.waitForFunction(()=>window.livlivVerification.status==='Microphone failed',undefined,{timeout:10000});
evidence.denied=await p.evaluate(()=>window.livlivVerification);
await p.waitForFunction(frame=>window.livlivVerification.frames>frame, evidence.denied.frames,{timeout:10000});
await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone');window.pendingPermission=null;navigator.mediaDevices.getUserMedia=()=>new Promise(resolve=>window.pendingPermission=resolve);window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');});
await p.waitForFunction(()=>!!window.pendingPermission,undefined,{timeout:10000});
await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone'));
await p.waitForFunction(()=>window.livlivVerification.status==='Microphone stopped',undefined,{timeout:10000});
await p.evaluate(()=>{const stream=window.testAudioDestination.stream.clone();window.cancelledTracks=stream.getTracks();window.pendingPermission(stream);});
await p.waitForFunction(()=>window.cancelledTracks.every(t=>t.readyState==='ended'),undefined,{timeout:10000});
evidence.cancel=await p.evaluate(async()=>{const result={player:window.livlivVerification,tracks:window.cancelledTracks.map(t=>t.readyState),contexts:window.captureContexts.map(c=>c.state)};navigator.mediaDevices.getUserMedia=window.savedGetUserMedia;window.AudioContext=window.savedAudioContext;await window.testAudioContext.close();document.querySelector('#generated-audio-test').remove();return result;});
evidence.sourceHashes={};for(const path of ['src/runtime/voice.ts','unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivBrowser.jslib','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLVoiceInput.cs','unity/Assets/Verification/PortVerification.cs'])evidence.sourceHashes[path]=crypto.createHash('sha256').update(await fs.readFile(root+path)).digest('hex');
await fs.writeFile(root+'unity/TestResults/webgl-browser.json',JSON.stringify(evidence,null,2));console.log(evidence);
