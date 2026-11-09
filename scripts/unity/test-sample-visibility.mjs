import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import vm from 'node:vm';
const code=readFileSync(new URL('../../unity/Assets/Verification/CaptureVisibility.jslib',import.meta.url),'utf8');
function fixture(){
 const document=new EventTarget(),window=new EventTarget(),calls=[],library={};document.hidden=false;
 const env={document,window,Module:{SendMessage:(...args)=>calls.push(args)},UTF8ToString:s=>s,LibraryManager:{library},mergeInto:Object.assign};
 vm.runInNewContext(code,env);env.LivLivSampleVisibility=library.$LivLivSampleVisibility;
 return {document,window,calls,library};
}
test('Visible focus/visibility does not suspend; hidden and pagehide suspend synchronously',()=>{
 const {document,window,calls,library}=fixture();library.LivLivSampleVisibility_Bind('Sample');
 window.dispatchEvent(new Event('blur'));document.dispatchEvent(new Event('visibilitychange'));assert.equal(calls.length,0);
 assert.equal(library.LivLivSampleVisibility_Hidden(),0);
 document.hidden=true;document.dispatchEvent(new Event('visibilitychange'));
 assert.deepEqual(calls,[['Sample','SuspendBrowserCapture']]);assert.equal(library.LivLivSampleVisibility_Hidden(),1);
 document.hidden=false;document.dispatchEvent(new Event('visibilitychange'));assert.equal(calls.length,1);
 window.dispatchEvent(new Event('pagehide'));assert.equal(calls.length,2);
});
test('Unbind removes both listeners and keeps another host binding intact',()=>{
 const {document,window,calls,library}=fixture(),a=library.LivLivSampleVisibility_Bind('A'),b=library.LivLivSampleVisibility_Bind('B');
 library.LivLivSampleVisibility_Unbind(a);library.LivLivSampleVisibility_Unbind(a);
 document.hidden=true;document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('pagehide'));
 assert.deepEqual(calls,[['B','SuspendBrowserCapture'],['B','SuspendBrowserCapture']]);
 library.LivLivSampleVisibility_Unbind(b);document.dispatchEvent(new Event('visibilitychange'));window.dispatchEvent(new Event('pagehide'));assert.equal(calls.length,2);
 assert.equal(Object.keys(library.$LivLivSampleVisibility.bindings).length,0);
});
