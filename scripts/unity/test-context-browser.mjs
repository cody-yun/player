import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
const source=fs.readFileSync(new URL('../../unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivContext.jslib',import.meta.url),'utf8')
 .replace("{{{ makeDynCall('vi', 'callback') }}}",'callback');
function setup(lost=false){
 const listeners=new Set();let adds=0,removes=0;
 const canvas={addEventListener(type,fn){assert.equal(type,'webglcontextlost');listeners.add(fn);adds++;},removeEventListener(type,fn){listeners.delete(fn);removes++;}};
 const context=vm.createContext({Module:{canvas},GL:{currentContext:{GLctx:{isContextLost:()=>lost}}},LibraryManager:{library:{}},mergeInto:(a,b)=>Object.assign(a,b)});
 vm.runInContext(source,context);const lib=context.LibraryManager.library;context.LivLivContext=lib.$LivLivContext;
 return {register:lib.LivLivContext_Register,remove:lib.LivLivContext_Unregister,
  lose(){lost=true;for(const listener of [...listeners])listener();},get stats(){return {adds,removes,listeners:listeners.size,entries:Object.keys(lib.$LivLivContext.entries).length};}};
}
test('one canvas listener notifies every live owner once',()=>{
 const s=setup(),seen=[];s.register(1,id=>seen.push(id));s.register(2,id=>seen.push(id));s.lose();s.lose();
 assert.deepEqual(seen,[1,2]);assert.equal(s.stats.adds,1);s.remove(1);assert.equal(s.stats.listeners,1);s.remove(2);assert.deepEqual(s.stats,{adds:1,removes:1,listeners:0,entries:0});
});
test('disposal during notification skips removed owners',()=>{
 const s=setup(),seen=[];s.register(1,id=>{seen.push(id);s.remove(2);s.remove(1);});s.register(2,id=>seen.push(id));s.lose();
 assert.deepEqual(seen,[1]);assert.equal(s.stats.listeners,0);
});
test('already lost context and late subscriptions notify immediately',()=>{
 const s=setup(true),seen=[];s.register(1,id=>seen.push(id));s.register(2,id=>seen.push(id));s.lose();assert.deepEqual(seen,[1,2]);
});
test('new owner after final disposal still observes actual context loss',()=>{
 const s=setup(),seen=[];s.register(1,id=>seen.push(id));s.lose();s.remove(1);s.register(2,id=>seen.push(id));s.remove(2);s.remove(2);
 assert.deepEqual(seen,[1,2]);assert.deepEqual(s.stats,{adds:2,removes:2,listeners:0,entries:0});
});
