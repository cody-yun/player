// Run the two original render-size statements, not a rewritten JS implementation.
import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url)),source=fs.readFileSync(root+'src/runtime/puppet.ts','utf8');
const start=source.indexOf('    const dpr = Math.min(this.maxDpr, window.devicePixelRatio || 1)',source.indexOf('  render() {'));
const end=source.indexOf('    if (cv.width',start);
if(start<0||end<0)throw Error('Upstream render-size statements moved');
const calculate=new Function('cv','window',source.slice(start,end)+'return [w,h]');
const cases=[];
for(const [width,height] of [[0,0],[0,1],[1.25,1.75],[320,568],[390,844],[844,390],[512.25,768.5],[1280,800]])
 for(const density of [0,.5,1,1.5,2,3])for(const cap of [1,2])cases.push({width,height,density,cap,expected:calculate.call({maxDpr:cap},{clientWidth:width,clientHeight:height},{devicePixelRatio:density})});
const data={sourceHashes:{'src/runtime/puppet.ts':crypto.createHash('sha256').update(source).digest('hex')},cases};
const output=JSON.stringify(data,null,2)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/surface-size.json';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('surface size fixture differs');console.log('surface-size fixture matches upstream')}else{fs.writeFileSync(dest,output);console.log('surface-size cases: '+cases.length)}
