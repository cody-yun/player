import fs from 'node:fs';import vm from 'node:vm';import assert from 'node:assert/strict';import test from 'node:test';
const code=fs.readFileSync(new URL('../../unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivCalibration.jslib',import.meta.url),'utf8');
function bridge(storage){const library={},heap={};vm.runInNewContext(code,{LibraryManager:{library},mergeInto:Object.assign,localStorage:storage,UTF8ToString:v=>v,lengthBytesUTF8:v=>Buffer.byteLength(v),stringToUTF8:(v,p,c)=>{assert.ok(Buffer.byteLength(v)+1<=c);heap[p]=v;}});return {library,heap};}
test('UTF-8 size includes terminator and original localStorage key is preserved',()=>{
 const values=new Map(),{library:l,heap}=bridge({getItem:k=>values.get(k)??null,setItem:(k,v)=>values.set(k,v)});
 const key='lm_calib:캐릭터',body='{"x":1,"note":"한글"}';assert.equal(l.LivLivCalibration_Read(key,0,0),0);l.LivLivCalibration_Write(key,body);
 const size=l.LivLivCalibration_Read(key,0,0);assert.equal(size,Buffer.byteLength(body)+1);assert.equal(l.LivLivCalibration_Read(key,12,size),size);assert.equal(heap[12],body);
 l.LivLivCalibration_Read(key,13,size-1);assert.equal(heap[13],undefined);
});
test('denied read and quota write are non-fatal',()=>{const {library:l}=bridge({getItem(){throw Error('denied')},setItem(){throw Error('quota')}});assert.equal(l.LivLivCalibration_Read('x',0,0),0);assert.doesNotThrow(()=>l.LivLivCalibration_Write('x','{}'));});
