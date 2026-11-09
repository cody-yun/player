import fs from 'node:fs';import crypto from 'node:crypto';import {fileURLToPath} from 'node:url';
const root=fileURLToPath(new URL('../../',import.meta.url)),source=fs.readFileSync(root+'src/runtime/puppet.ts','utf8');
const start=source.indexOf('    let x0 = Infinity',source.indexOf('  private drawMasks(')),end=source.indexOf('    gl.activeTexture',start);
const growth=source.match(/const W = Math.max\(this.maskSize\[0\].* H = .*\n/)[0];
if(start<0||end<0||!growth)throw Error('Upstream mask bounds moved');
const calc=new Function('eyes','w','h','sx','sy','ox','oy',source.slice(start,end)+growth+'return {x:x0,y:y0,width:x1-x0,height:y1-y0,storage:[W,H]}');
const cases=[];
const shapes=[[],[[0,0,10,0,10,10,0,10]],[[30,40,45,40,45,48,30,48],[60,39,75,39,75,50,60,50]],[[1.25,2.75,6.125,2.75,6.125,8.875]],[[0,0,100,0,100,100,0,100]]];
for(const [w,h] of [[64,64],[512,768],[333,127],[1,3]])for(const [sx,sy,ox,oy] of [[.02,.02,-1,-1],[.014,.018,-.7,-.9],[.02,.02,-4,-1],[.02,.02,1.2,.8]])for(const previous of [[0,0],[128,64]])for(const raw of shapes){
 const points=raw.map(v=>Array.from(new Float32Array(v)));const eyes=points.map(out=>({out,maskCh:0}));
 const result=calc.call({maskSize:previous},eyes,w,h,sx,sy,ox,oy);
 cases.push({w,h,view:[sx,sy,ox,oy],previous,points,expected:result??null});
}
const data={sourceHashes:{'src/runtime/puppet.ts':crypto.createHash('sha256').update(source).digest('hex')},cases};
const output=JSON.stringify(data,null,2)+'\n',dest=root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/mask-bounds.json';
if(process.argv.includes('--check')){if(fs.readFileSync(dest,'utf8')!==output)throw Error('mask fixture differs');console.log('mask bounds fixture matches upstream')}else{fs.writeFileSync(dest,output);console.log('mask bounds cases: '+cases.length)}
