// Run through ego-browser nodejs with root (absolute, trailing slash) and soakSpaceId injected.
// Generated real MediaStreams only. Browser heap metrics are diagnostic, not a native leak verdict.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const directory=root+'unity/TestResults/webgl-capture-soak/'+Date.now();
await fs.mkdir(directory,{recursive:false});
const task=await taskSpace(soakSpaceId),p=task.page('p1');
const rows=[],report={result:'Running',scope:'30 generated camera/audio start-stop cycles in desktop Chrome; no hardware permission, no native memory verdict',spaceId:soakSpaceId,rows,sourceHashes:{},buildHashes:{}};
const hash=async file=>crypto.createHash('sha256').update(await fs.readFile(root+file)).digest('hex');
for(const name of ['scripts/unity/verify-webgl-capture-soak.mjs','unity/Assets/Verification/PortVerification.cs','unity/Packages/ai.livliv.player/Browser~/face-input.mjs','unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivBrowser.jslib'])report.sourceHashes[name]=await hash(name);
for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))report.buildHashes[name]=await hash('unity/TestResults/WebGL/Build/'+name);
const save=async()=>fs.writeFile(directory+'/progress.json',JSON.stringify(report,null,2)+'\n');
const read=()=>p.evaluate(()=>{
 const s=window.captureSoak,e=window.livlivVerification;
 return {player:e,time:performance.now(),visibility:document.visibilityState,trackCount:s.tracks.length,
  liveVideo:s.tracks.filter(t=>t.kind==='video'&&t.readyState==='live').length,
  liveAudio:s.tracks.filter(t=>t.kind==='audio'&&t.readyState==='live').length,
  contextCount:s.contexts.length,openContexts:s.contexts.filter(c=>c.state!=='closed').length,
  usedJsHeapBytes:performance.memory?.usedJSHeapSize??null,
  scrollX:document.documentElement.scrollWidth>innerWidth,scrollY:document.documentElement.scrollHeight>innerHeight};
});
try{
 await p.goto('http://127.0.0.1:8770/?capture-soak='+Date.now());
 await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
 await p.evaluate(async()=>{
  const image=new Image();image.src='/portrait.jpg';await image.decode();
  const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;
  const draw=()=>canvas.getContext('2d').drawImage(image,0,0,820,615,0,0,640,480);draw();
  const s=window.captureSoak={tracks:[],contexts:[],timer:setInterval(draw,33)};
  const Context=window.AudioContext;s.originalContext=Context;s.originalCapture=navigator.mediaDevices.getUserMedia;
  window.AudioContext=function(options){const c=new Context(options);s.contexts.push(c);return c};
  const button=document.createElement('button');button.id='capture-soak-start';button.textContent='Start generated capture soak';
  Object.assign(button.style,{position:'fixed',top:'4px',left:'4px',zIndex:9999});
  button.onclick=async()=>{
   s.generator=new Context();s.osc=s.generator.createOscillator();const gain=s.generator.createGain();
   const dest=s.generator.createMediaStreamDestination();s.osc.frequency.value=650;gain.gain.value=.2;
   s.osc.connect(gain);gain.connect(dest);s.osc.start();await s.generator.resume();
   s.generatorTracks=dest.stream.getTracks();
   navigator.mediaDevices.getUserMedia=constraints=>{
    const stream=constraints.video?canvas.captureStream(30):dest.stream.clone();s.tracks.push(...stream.getTracks());return Promise.resolve(stream);
   };s.ready=true;
  };document.body.prepend(button);
 });
 console.log(await p.snapshot());await p.click('#capture-soak-start',{label:'Starting generated capture soak'});
 await p.waitForFunction(()=>window.captureSoak.ready,undefined,{timeout:10000});
 report.userAgent=await p.evaluate(()=>navigator.userAgent);rows.push({cycle:-1,stage:'baseline',...await read()});await save();
 for(let cycle=0;cycle<30;cycle++){
  await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StartFace');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone')});
  await p.waitForFunction(()=>{const e=window.livlivVerification;return e?.tracking&&e.faceSeen&&e.microphone&&e.mouth>.05},undefined,{timeout:45000});
  const active={cycle,stage:'active',...await read()};rows.push(active);
  if(active.liveAudio!==1||active.liveVideo!==1||active.openContexts!==1)throw Error('Unexpected active capture resources: '+JSON.stringify(active));
  await p.evaluate(()=>{window.captureSoak.frameAtActive=window.livlivVerification.frames});
  await p.waitForFunction(()=>window.livlivVerification.frames>=window.captureSoak.frameAtActive+60,undefined,{timeout:15000});
  await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopFace');window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone')});
  await p.waitForFunction(()=>{const e=window.livlivVerification,s=window.captureSoak;return !e.tracking&&!e.microphone&&s.tracks.every(t=>t.readyState==='ended')&&s.contexts.every(c=>c.state==='closed')},undefined,{timeout:10000});
  rows.push({cycle,stage:'stopped',...await read()});
  if(rows.slice(-2).some(r=>r.player.scale!==1||r.player.expressionCount!==4||r.visibility!=='visible'||r.scrollX||r.scrollY))throw Error('Scale, metadata, visibility or no-scroll invariant failed');
  await save();console.log(JSON.stringify({directory,cycle,completed:cycle+1,last:rows.at(-1)}));
 }
 report.result='Passed';report.verifiedAtUtc=new Date().toISOString();
}catch(error){report.result='Failed';report.error=String(error);throw error}
finally{
 await p.evaluate(async()=>{
  const s=window.captureSoak;if(!s)return;
  window.livlivUnity?.SendMessage('LivLivVerification','StopFace');window.livlivUnity?.SendMessage('LivLivVerification','StopMicrophone');
  clearInterval(s.timer);for(const t of [...s.tracks,...(s.generatorTracks||[])])t.stop();
  s.osc?.stop();if(s.generator)await s.generator.close();
  navigator.mediaDevices.getUserMedia=s.originalCapture;window.AudioContext=s.originalContext;
 });
 report.cleanup=await read();
 report.generatorClosed=await p.evaluate(()=>window.captureSoak.generator.state==='closed'&&window.captureSoak.generatorTracks.every(t=>t.readyState==='ended'));
 if(report.cleanup.liveAudio||report.cleanup.liveVideo||report.cleanup.openContexts||!report.generatorClosed){report.result='Failed';report.error='Final cleanup incomplete'}
 await fs.writeFile(directory+'/evidence.json',JSON.stringify(report,null,2)+'\n',{flag:'wx'});
 await p.goto('http://127.0.0.1:8770/?capture-soak-recovered='+Date.now());
 await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
 console.log(JSON.stringify({directory,result:report.result,rows:rows.length,generatorClosed:report.generatorClosed,recovery:await p.evaluate(()=>window.livlivVerification)}));
}
if(report.result!=='Passed')throw Error(report.error||'Capture soak failed');
