// Execute upstream PlayerBase.start, not a second implementation of its progress rules.
import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
import {PlayerBase} from '../../src/base.ts';import {defaultParams} from '../../src/runtime/params.ts';import {readModel} from '../../src/runtime/format.ts';
const root=fileURLToPath(new URL('../../',import.meta.url));
globalThis.cancelAnimationFrame=()=>{};globalThis.addEventListener=()=>{};globalThis.removeEventListener=()=>{};globalThis.document={addEventListener(){},removeEventListener(){}};
class Host extends PlayerBase{
 constructor(){super({addEventListener(){},removeEventListener(){},style:{getPropertyValue(){return ''},setProperty(){}}},defaultParams(),{autoplay:false,lang:'en'})}
 get drawable(){return false}get clock(){return 0}get shownExpr(){return {}}seen(){return {}}get gl(){return null}show(){}free(){}
}
const cases=[[],[0,.2,.1,.2,.8,'build',.9,'build',1],[0,'build'],['build',0,1],[0,1,'build'],[.7,.8,'build']];
const progress=[];for(const calls of cases){const p=new Host(),expected=[];await p.start({onProgress:e=>expected.push(e)},async job=>{for(const c of calls)c==='build'?job.build():job.progress(c)});p.dispose();progress.push({calls,expected})}
const versions=[];for(const name of fs.readdirSync(root+'characters').sort()){const path=root+'characters/'+name+'/model.json';if(!fs.existsSync(path))continue;const model=readModel(JSON.parse(fs.readFileSync(path,'utf8')));const lastModified='Mon, 09 Nov 2026 00:00:00 GMT';let hash=0;for(const c of (JSON.stringify(model.layers)+lastModified).split(''))hash=(hash*31+c.charCodeAt(0))|0;versions.push({name,lastModified,expected:(hash>>>0).toString(36)})}
const data={sourceHashes:Object.fromEntries(['src/base.ts','src/runtime/puppet.ts','src/runtime/format.ts'].map(p=>[p,crypto.createHash('sha256').update(fs.readFileSync(root+p)).digest('hex')])),progress,versions};
const output=JSON.stringify(data,null,2)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/loading.json';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('loading fixture differs');console.log('loading fixture matches upstream')}else{fs.writeFileSync(dest,output);console.log(`loading: ${progress.length} progress sequences, ${versions.length} version hashes`)}
