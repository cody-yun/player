// Inject absolute root and existing authorized traceSpaceId; no new space or device activation.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto'),zlib=await import('node:zlib');
const p=(await taskSpace(traceSpaceId)).page('p1');await p.goto('http://127.0.0.1:8773/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
await p.evaluate(()=>{delete window.livlivSnapshot;window.livlivUnity.SendMessage('LivLivVerification','TraceProbe');});
await p.waitForFunction(()=>!!window.livlivSnapshot,undefined,{timeout:15000});const report=await p.evaluate(()=>window.livlivSnapshot);
if(report.result!=='Passed')throw Error(JSON.stringify(report));
for(const row of report.states){const body=JSON.parse(row.record);if(body.length!==1441||body[0].t!==0||body.at(-1).t!==48000)throw Error('Trace export lost frames');await fs.writeFile(root+'unity/TestResults/webgl-trace-'+(row.narrow?'narrow':'normal')+'.json',row.record);delete row.record;}
const expected=JSON.parse(zlib.gunzipSync(await fs.readFile(root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/traces.json.gz'))).pipeline;
if(JSON.stringify(report.states)!==JSON.stringify(expected))throw Error('Actual WASM differs from original '+JSON.stringify({actual:report.states,expected}));
report.sourceHashes={};for(const name of ['src/runtime/trace.ts','src/runtime/tracking.ts','unity/Packages/ai.livliv.player/Runtime/runtime/trace.cs','unity/Packages/ai.livliv.player/Runtime/runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/tracking.cs','unity/Packages/ai.livliv.player/Runtime/Platform/TraceInput.cs','unity/Assets/Verification/PortVerification.Trace.cs'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
report.sample=await p.evaluate(()=>({state:window.livlivVerification,viewport:[innerWidth,innerHeight],document:[document.documentElement.scrollWidth,document.documentElement.scrollHeight]}));
await fs.writeFile(root+'unity/TestResults/webgl-trace.json',JSON.stringify(report,null,2)+'\n');console.log(report);
