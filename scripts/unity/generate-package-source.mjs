// Expose the unchanged upstream private fromFiles through a transient Node loader hook.
// No source file is rewritten and no second package-selection implementation is used as the oracle.
import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {registerHooks} from 'node:module';import {gunzipSync} from 'node:zlib';import {execFileSync} from 'node:child_process';
const root=fileURLToPath(new URL('../../',import.meta.url)),index=new URL('../../src/index.ts',import.meta.url).href;
const hook=registerHooks({load(url,context,next){const result=next(url,context);return url===index?{...result,source:String(result.source)+'\nexport { fromFiles as packageSourceOracle };\n'}:result}});
const {packageSourceOracle}=await import(index);hook.deregister();
const format=JSON.parse(gunzipSync(fs.readFileSync(root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/format.json.gz')));
const model=format.models.find(m=>m.name==='minimal-v4').input;
const bytes=s=>Buffer.from(s),json=bytes(JSON.stringify(model));
const zip=entries=>Buffer.from(execFileSync('python3',['-c',
 'import sys,json,io,zipfile,base64;f=io.BytesIO();z=zipfile.ZipFile(f,"w");[(z.writestr(zipfile.ZipInfo(k,date_time=(2026,1,1,0,0,0)),base64.b64decode(v),compress_type=zipfile.ZIP_DEFLATED)) for k,v in json.load(sys.stdin).items()];z.close();sys.stdout.write(base64.b64encode(f.getvalue()).decode())'],
 {input:JSON.stringify(Object.fromEntries(Object.entries(entries).map(([k,v])=>[k,v.toString('base64')])))}).toString(),'base64');
const archive=zip({'Wrapped/model.json':json,'Wrapped/keyforms.json':bytes('{"version":1,"keys":[]}')});
const invalidZip=bytes('PK\x03\x04broken'),glb=Buffer.from([0x67,0x6c,0x54,0x46,2,0,0,0]);
const definitions=[
 ['direct-json','files',{'model.json':json}],
 ['normalized-json','files',{'./\\Wrapped\\model.json':json}],
 ['picked-package','files',{'avatar.livliv':archive}],
 ['picked-package-unusual-extension','files',{'avatar.dat':archive,'note.txt':bytes('note')}],
 ['picked-package-ignores-outer-files','files',{'note.txt':bytes('note'),'avatar.zip':archive,'model.vrm':glb}],
 ['blob-package','blob',archive],
 ['json-takes-precedence','files',{'model.json':json,'bad.zip':invalidZip,'other.glb':glb}],
 ['shallowest-json','files',{'longer/model.json':bytes('{}'),'model.json':json}],
 ['first-zip-is-authoritative','files',{'bad.zip':invalidZip,'good.zip':archive}],
 ['no-json','files',{'note.txt':bytes('note')}],
 ['vrm-name','files',{'Avatar.VRM':bytes('unknown')}],
 ['glb-name','files',{'folder/avatar.glb':glb}],
 ['gltf-name-is-not-special','files',{'avatar.gltf':bytes('{}')}],
 ['blob-glb-magic','blob',glb],
 ['blob-short-magic','blob',glb.subarray(0,3)],
 ['blob-not-zip','blob',bytes('ordinary file')],
 ['json-before-vrm','files',{'model.json':bytes('{}'),'avatar.vrm':glb}],
 ['blob-nested-zip-not-recursive','blob',zip({'inner.livliv':archive})],
 ['picked-nested-zip-not-recursive','files',{'outer.livliv':zip({'inner.livliv':archive})}],
 ['zip-vrm-name','blob',zip({'avatar.VrM':glb})],
 ['first-valid-zip-is-authoritative','files',{'first.livliv':zip({'note.txt':bytes('note')}),'second.livliv':archive}],
 ['zip-json-before-vrm','blob',zip({'model.json':json,'avatar.glb':glb})],
];
const cases=[];
for(const [name,kind,input] of definitions){
 const files=kind==='blob'?null:Object.fromEntries(Object.entries(input).map(([k,v])=>[k,v.toString('base64')]));
 const source=kind==='blob'?new Blob([input]):new Map(Object.entries(input).map(([k,v])=>[k,new Blob([v])]));
 let expected;
 try{const result=await packageSourceOracle(source);expected={model:result.model,keyforms:result.keyforms??null,error:null}}
 catch(e){expected={error:e.code??e.name,message:e.message}}
 cases.push({name,kind,...(files?{files}:{bytes:input.toString('base64')}),expected});
}
const data={sourceHashes:Object.fromEntries(['src/index.ts','src/zip.ts','src/runtime/format.ts','src/errors.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),cases};
const dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/package-source.json',output=JSON.stringify(data,null,2)+'\n';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('package-source fixture differs');console.log('package-source fixture matches upstream')}
else{fs.writeFileSync(dest,output);console.log('package-source: '+cases.length+' upstream cases')}
