// ego-browser: inject absolute root (trailing slash) and authorized releaseSpaceId.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto'),path=await import('node:path');
const output=typeof releaseReportPath!=='undefined'?releaseReportPath:root+'unity/TestResults/release-browser/'+Date.now()+'.json';
await fs.mkdir(path.dirname(output),{recursive:true});
try{await fs.access(output);throw Error('Refusing to overwrite release evidence')}catch(error){if(error.code!=='ENOENT')throw error}
const p=(await taskSpace(releaseSpaceId)).page('p1');await p.goto('http://127.0.0.1:8770/?release='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
await p.evaluate(()=>{delete window.livlivSnapshot;window.livlivUnity.SendMessage('LivLivVerification','ReleaseTargetProbe','')});
await p.waitForFunction(()=>!!window.livlivSnapshot,undefined,{timeout:20000});
const report=await p.evaluate(()=>window.livlivSnapshot);
if(report.result!=='Passed'||report.platform!=='WebGLPlayer'||report.graphics!=='OpenGLES3')throw Error(JSON.stringify(report));
for(const key of ['defaultRetained','immediateClaimCanceled','completedReleased','releasedRejected','recreatedRejected','otherRendered','mainAlive','targetObjectRetained','sharedOwnersNotified','retargetedOwnerPreserved'])if(!report[key])throw Error(key+' failed');
if(report.otherActivePixels<=50||report.mainEndFrame<=report.mainStartFrame)throw Error('Independent renderer or main loop stopped');
await p.waitForFunction(end=>window.livlivVerification.frames>end,report.mainEndFrame,{timeout:10000});
const evidence={result:'Passed',scope:'Actual WASM logical RenderTexture release; separate player PNG alpha and main loop survive; Unity shared WebGL context retained',spaceId:releaseSpaceId,report,
               main:await p.evaluate(()=>window.livlivVerification),sourceHashes:{},buildHashes:{}};
for(const name of ['src/release.ts','unity/Packages/ai.livliv.player/Runtime/release.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.Release.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Assets/Verification/PortVerification.Release.cs'])evidence.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))evidence.buildHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/WebGL/Build/'+name)).digest('hex');
await fs.writeFile(output,JSON.stringify(evidence,null,2)+'\n',{flag:'wx'});console.log({output,report});
await p.goto('http://127.0.0.1:8770/?release-recovered='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
