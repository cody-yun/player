// ego-browser: inject root (absolute repository path with trailing slash) and deviceSpaceId.
// The ended event is injected on generated tracks, never on a physical device.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto'),path=await import('node:path');
const reportPath=typeof deviceReportPath!=='undefined'?deviceReportPath:root+'unity/TestResults/device-ended/runs/'+Date.now()+'.json';
await fs.mkdir(path.dirname(reportPath),{recursive:true});
try{await fs.access(reportPath);throw Error('Refusing to overwrite browser evidence: '+reportPath)}catch(error){if(error.code!=='ENOENT')throw error}
const p=(await taskSpace(deviceSpaceId)).page('p1');
await p.goto('http://127.0.0.1:8770/?device-ended='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
await p.evaluate(async()=>{
 const photo=new Image();photo.src='/portrait.jpg';await photo.decode();
 const canvas=document.createElement('canvas');canvas.width=640;canvas.height=480;
 const draw=()=>canvas.getContext('2d').drawImage(photo,0,0,820,615,0,0,640,480);draw();
 const s=window.deviceEnded={tracks:[],contexts:[],timer:setInterval(draw,33)};
 const Context=window.AudioContext;
 window.AudioContext=function(options){const c=new Context(options);s.contexts.push(c);return c};
 const button=document.createElement('button');button.id='device-ended-start';button.textContent='Start generated capture';
 Object.assign(button.style,{position:'fixed',top:'4px',left:'4px',zIndex:9999});
 button.onclick=async()=>{
  s.generator=new Context();s.osc=s.generator.createOscillator();const gain=s.generator.createGain();
  const dest=s.generator.createMediaStreamDestination();s.osc.frequency.value=650;gain.gain.value=.2;
  s.osc.connect(gain);gain.connect(dest);s.osc.start();await s.generator.resume();
  navigator.mediaDevices.getUserMedia=constraints=>{
   const stream=constraints.video?canvas.captureStream(30):dest.stream.clone();s.tracks.push(...stream.getTracks());return Promise.resolve(stream);
  };
  window.livlivUnity.SendMessage('LivLivVerification','StartFace');window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone');
 };
 document.body.prepend(button);
});
console.log(await p.snapshot());await p.click('#device-ended-start',{label:'Starting generated camera and microphone'});
const active=()=>p.waitForFunction(()=>{const s=window.livlivVerification;return s?.tracking&&s.faceSeen&&s.microphone&&s.mouth>.05},undefined,{timeout:60000});
const read=()=>p.evaluate(()=>({player:window.livlivVerification,tracks:window.deviceEnded.tracks.map(t=>({kind:t.kind,state:t.readyState})),contexts:window.deviceEnded.contexts.map(c=>c.state),faceModuleHasEnded:window.LivLivFaceApi?.start.toString().includes('addEventListener'),moduleUrls:[...document.querySelectorAll('script[type=module]')].map(s=>s.src)}));
const rows=[];
try{
 await active();rows.push({stage:'active',...await read()});
 if(!rows[0].faceModuleHasEnded||!rows[0].moduleUrls.some(url=>new URL(url).searchParams.get('build')))throw Error('Default face module is stale or has no build fingerprint');
 await p.evaluate(()=>{
  for(const t of window.deviceEnded.tracks.filter(t=>t.kind==='audio'&&t.readyState==='live')){t.stop();t.dispatchEvent(new Event('ended'))}
 });
 await p.waitForFunction(()=>!window.livlivVerification.microphone&&window.livlivVerification.tracking&&window.deviceEnded.contexts.every(c=>c.state==='closed'),undefined,{timeout:10000});
 rows.push({stage:'microphone-ended',...await read()});
 if(rows.at(-1).player.status!=='Microphone failed')throw Error('Missing microphone failure state');
 await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StartMicrophone'));await active();
 rows.push({stage:'microphone-restarted',...await read()});
 await p.evaluate(()=>{
  for(const t of window.deviceEnded.tracks.filter(t=>t.kind==='video'&&t.readyState==='live')){t.stop();t.dispatchEvent(new Event('ended'))}
 });
 await p.waitForFunction(()=>!window.livlivVerification.tracking&&window.livlivVerification.microphone,undefined,{timeout:10000});
 rows.push({stage:'camera-ended',...await read()});
 if(!rows.at(-1).player.faceStatus.startsWith('Failed:'))throw Error('Missing camera failure state');
 await p.evaluate(()=>window.livlivUnity.SendMessage('LivLivVerification','StartFace'));await active();
 rows.push({stage:'camera-restarted',...await read()});
 await p.evaluate(()=>{window.livlivUnity.SendMessage('LivLivVerification','StopFace');window.livlivUnity.SendMessage('LivLivVerification','StopMicrophone')});
 await p.waitForFunction(()=>!window.livlivVerification.tracking&&!window.livlivVerification.microphone&&window.deviceEnded.tracks.every(t=>t.readyState==='ended')&&window.deviceEnded.contexts.every(c=>c.state==='closed'),undefined,{timeout:10000});
 rows.push({stage:'stopped',...await read()});
 if(rows.some(row=>row.player.expressionCount!==4||!row.player.expressionLabel||row.player.expressionTranslation!=='Smile'))throw Error('Typed expression metadata was not preserved in every capture state');
 const report={result:'Passed',scope:'Generated real MediaStreams; injected ended events, no physical unplug',rows,sourceHashes:{},buildHashes:{}};
 for(const name of ['unity/Assets/Verification/PortVerification.cs','unity/Packages/ai.livliv.player/Browser~/face-input.mjs','unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivBrowser.jslib','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLFaceInput.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.player/compatibility.json'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
 for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))report.buildHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/WebGL/Build/'+name)).digest('hex');
 report.spaceId=deviceSpaceId;report.verifiedAtUtc=new Date().toISOString();
 await fs.writeFile(reportPath,JSON.stringify(report,null,2)+'\n',{flag:'wx'});console.log(JSON.stringify({reportPath,report}));
}catch(error){
 const failed={result:'Failed',error:String(error),rows,current:await read()};
 await fs.writeFile(reportPath+'.failure.json',JSON.stringify(failed,null,2)+'\n',{flag:'wx'});
 console.log(JSON.stringify(failed));throw error;
}finally{
 await p.evaluate(async()=>{const s=window.deviceEnded;clearInterval(s.timer);for(const t of s.tracks)t.stop();s.osc.stop();await s.generator.close()});
 await p.goto('http://127.0.0.1:8770/?device-ended-recovered='+Date.now());
 await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
}
