// ego-browser: inject root and authorized textMarkSpaceId; no new space or device activation.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const output=typeof textMarkOutput!=='undefined'?textMarkOutput:root+'unity/TestResults/webgl-text-mark/'+Date.now();
await fs.mkdir((await import('node:path')).dirname(output),{recursive:true});await fs.mkdir(output,{recursive:false});
const p=(await taskSpace(textMarkSpaceId)).page('p1');await p.goto('http://127.0.0.1:8773/?text-mark='+Date.now());
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const report={at:new Date().toISOString(),browser:await p.evaluate(()=>navigator.userAgent),states:[],sourceHashes:{}};
for(const name of ['empty','latin','korean','literal','default','retina-empty','retina','budget']){
 await p.evaluate(name=>{delete window.livlivSnapshot;window.livlivUnity.SendMessage('LivLivVerification','TextMarkFrame',name);},name);
 await p.waitForFunction(()=>!!window.livlivSnapshot,undefined,{timeout:15000});
 const r=await p.evaluate(()=>window.livlivSnapshot);
 if(r.watermarks||r.coverageTargets||r.coverageMeshes||r.coverageMaterials)throw Error('Text resources retained '+JSON.stringify({...r,png:undefined}));
 if(name==='budget'){if(r.code!=='too-large')throw Error('Text budget failed '+JSON.stringify({...r,png:undefined}));r.textLength=r.text.length;delete r.text;}
 else{
  if(r.error||!r.png||r.font!==(name==='default'?'LivLivMarkFont':'NanumGothic-Regular'))throw Error('Text mark failed '+JSON.stringify({...r,png:undefined}));
  await fs.writeFile(output+'/webgl-text-'+name+'.png',Buffer.from(r.png,'base64'),{flag:'wx'});
 }
 delete r.png;report.states.push(r);
}
for(const name of ['src/mark.ts','src/runtime/brand.ts','unity/Packages/ai.livliv.player/Runtime/Platform/IMarkTextRasterizer.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivMarkFont.ttf','unity/Packages/ai.livliv.player/Runtime/Platform/BrandRasterizer.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityMark.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnityMarkText.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Resources/LivLivTextCoverage.shader','unity/Assets/Verification/PortVerification.TextMark.cs','unity/Assets/Verification/Fonts/NanumGothic-Regular.ttf'])report.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
report.spaceId=textMarkSpaceId;report.output=output;report.buildHashes={};
for(const name of await fs.readdir(root+'unity/TestResults/WebGL/Build'))report.buildHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+'unity/TestResults/WebGL/Build/'+name)).digest('hex');
await fs.writeFile(output+'/browser.json',JSON.stringify(report,null,2)+'\n',{flag:'wx'});console.log({output,states:report.states});
