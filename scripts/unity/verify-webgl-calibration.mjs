// Inject absolute root and existing authorized calibrationSpaceId. No capture device activation.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(calibrationSpaceId)).page('p1'),id='qa-calibration-'+crypto.randomUUID(),key='lm_calib:'+id;
const report={at:new Date().toISOString(),states:[],sourceHashes:{}};
async function load(){await p.goto('http://127.0.0.1:8773/');await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});}
async function probe(phase){
 await p.evaluate(input=>{delete window.livlivSnapshot;window.livlivUnity.SendMessage('LivLivVerification','CalibrationProbe',JSON.stringify(input));},{phase,id});
 await p.waitForFunction(()=>!!window.livlivSnapshot,undefined,{timeout:10000});const r=await p.evaluate(()=>window.livlivSnapshot);
 if(r.result!=='Passed')throw Error(JSON.stringify(r));report.states.push(r);return r;
}
await load();
try{
 const written=await probe('write');const stored=await p.evaluate(key=>localStorage.getItem(key),key);
 if(stored!==written.stored)throw Error('Not stored in original localStorage key');
 await load();const restored=await probe('restore');if(restored.stored!==stored)throw Error('Reload changed calibration');
 await probe('legacy');await load();await probe('restore');await probe('malformed');
 report.sample=await p.evaluate(()=>({state:window.livlivVerification,viewport:[innerWidth,innerHeight],document:[document.documentElement.scrollWidth,document.documentElement.scrollHeight]}));
}finally{if(!await p.evaluate(key=>{localStorage.removeItem(key);return localStorage.getItem(key)===null;},key))throw Error("QA cleanup failed");}
for(const name of ['src/runtime/tracking.ts','src/tracking.ts','unity/Packages/ai.livliv.player/Runtime/runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/Platform/ICalibrationStore.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityCalibrationStore.cs','unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivCalibration.jslib','unity/Assets/Verification/PortVerification.Calibration.cs','unity/Assets/Verification/PortVerification.cs'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
report.cleanup=true;await fs.writeFile(root+'unity/TestResults/webgl-calibration.json',JSON.stringify(report,null,2)+'\n');console.log(report);
