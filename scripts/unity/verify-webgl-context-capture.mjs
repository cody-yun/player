// ego-browser: inject root and contextSpaceId. Generated video/audio only, no hardware permission.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const pendingMode=typeof pendingCapture!=='undefined'&&pendingCapture;
const p=(await taskSpace(contextSpaceId)).page('p1');await p.goto('http://127.0.0.1:8770/?capture-loss='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
await p.evaluate(async pendingMode=>{
 const photo=new Image();photo.src='/portrait.jpg';await photo.decode();
 const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;
 const draw=()=>canvas.getContext('2d').drawImage(photo,0,0,820,615,0,0,640,480);draw();
 window.captureLossTimer=setInterval(draw,33);window.captureLossTracks=[];window.captureLossContexts=[];window.captureLossPending=[];window.captureLossRequests=0;
 const Context=window.AudioContext;window.captureLossOriginalContext=Context;
 window.AudioContext=function(options){const c=new Context(options);window.captureLossContexts.push(c);return c;};
 const button=document.createElement('button');button.id='capture-loss-start';button.textContent='Start generated capture';
 Object.assign(button.style,{position:'fixed',top:'4px',left:'4px',zIndex:9999});
 button.onclick=async()=>{
  const generator=window.captureLossGenerator=new Context(),osc=window.captureLossOscillator=generator.createOscillator();
  const gain=generator.createGain(),destination=generator.createMediaStreamDestination();osc.frequency.value=650;gain.gain.value=.2;
  osc.connect(gain);gain.connect(destination);osc.start();await generator.resume();
  window.captureLossMakeStream=constraints=>{
   const stream=constraints.video?canvas.captureStream(30):destination.stream.clone();
   window.captureLossTracks.push(...stream.getTracks());return stream;
  };
  navigator.mediaDevices.getUserMedia=constraints=>{
   window.captureLossRequests++;
   if(pendingMode)return new Promise(resolve=>window.captureLossPending.push({resolve,constraints}));
   return Promise.resolve(window.captureLossMakeStream(constraints));
  };
  window.livlivUnity.SendMessage('LivLivVerification','StartFace');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');
 };
 document.body.prepend(button);
},pendingMode);
console.log(await p.snapshot());await p.click('#capture-loss-start',{label:'Starting generated camera and microphone'});
if(pendingMode)await p.waitForFunction(()=>window.captureLossPending.length===2,undefined,{timeout:60000});
else await p.waitForFunction(()=>{const s=window.livlivVerification;return s?.tracking&&s.faceSeen&&s.microphone&&s.mouth>.05;},undefined,{timeout:60000});
const read=()=>p.evaluate(()=>({player:window.livlivVerification,requests:window.captureLossRequests,tracks:window.captureLossTracks.map(t=>({kind:t.kind,state:t.readyState})),contexts:window.captureLossContexts.map(c=>c.state)}));
const before=await read();
await p.evaluate(()=>{
 window.livlivUnity.SendMessage('LivLivVerification','ArmContextLossProbe');
 const gl=document.querySelector('#unity-canvas').getContext('webgl2');window.captureLossGL=gl;
 const extension=gl.getExtension('WEBGL_lose_context');if(!extension)throw Error('No context-loss extension');extension.loseContext();
});
await p.waitForFunction(()=>window.livlivVerification.status==='Context lost'&&window.captureLossTracks.every(t=>t.readyState==='ended')&&window.captureLossContexts.every(c=>c.state==='closed'),undefined,{timeout:10000});
if(pendingMode){
 await p.evaluate(()=>{for(const item of window.captureLossPending)item.resolve(window.captureLossMakeStream(item.constraints));});
 await p.waitForFunction(()=>window.captureLossTracks.length===2&&window.captureLossTracks.every(t=>t.readyState==='ended'),undefined,{timeout:10000});
}
const after=await read(),managed=await p.evaluate(()=>window.livlivContextLoss);
if(after.player.microphone||after.player.tracking||!managed.mainDisposed||!await p.evaluate(()=>window.captureLossGL.isContextLost()))throw Error('Sample retained capture after loss');
await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StartFace');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');});
const restarted=await read();if(restarted.requests!==after.requests||restarted.tracks.length!==after.tracks.length||restarted.player.microphone||restarted.player.tracking)throw Error('Lost sample restarted capture');
const report={result:'Passed',pendingMode,input:'Generated portrait canvas and oscillator audio; no hardware',before,after,managed,restarted,sourceHashes:{},buildHashes:{}};
for(const name of ['unity/Assets/Verification/PortVerification.cs','unity/Assets/Verification/PortVerification.ContextLoss.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLContextLoss.cs'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))report.buildHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/WebGL/Build/'+name)).digest('hex');
await fs.writeFile(root+'unity/TestResults/context-loss/webgl/'+(pendingMode?'capture-pending-browser.json':'capture-browser.json'),JSON.stringify(report,null,2)+'\n');console.log(JSON.stringify({result:report.result,pendingMode,after,managed}));
await p.evaluate(async()=>{clearInterval(window.captureLossTimer);window.captureLossOscillator.stop();await window.captureLossGenerator.close();});
await p.goto('http://127.0.0.1:8770/?capture-loss-recovered='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
console.log(await p.evaluate(()=>window.livlivVerification));
