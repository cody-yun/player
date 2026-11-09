// ego-browser: inject absolute root and authorized posterSpaceId. Uses the existing page only.
const fs=await import('node:fs/promises'),crypto=await import('node:crypto');
const p=(await taskSpace(posterSpaceId)).page('p1');await p.goto('http://127.0.0.1:8773/');
await p.waitForFunction(()=>window.livlivVerification?.frames>15,undefined,{timeout:45000});
const initial=await p.evaluate(()=>window.livlivVerification);
if(initial.microphone||initial.tracking)throw Error('Poster probe requires capture off');
const results={at:new Date().toISOString(),browser:await p.evaluate(()=>navigator.userAgent),states:{}};
const stamp=Date.now();const id=name=>name+'-'+stamp;
const send=(method,value='')=>p.evaluate(({method,value})=>window.livlivUnity.SendMessage('LivLivVerification',method,value),{method,value});
const load=(name,poster,gate=true)=>send('LoadPosterModel',JSON.stringify({id:name,url:'http://127.0.0.1:8773/'+(gate?'model.zip?id='+id(name):'missing.zip'),poster}));
const release=name=>p.evaluate(key=>fetch('/release?id='+key).then(r=>r.text()),id(name));
const reset=()=>send('ResetPosterProbe');
const wait=fn=>p.waitForFunction(fn,undefined,{timeout:15000});
const read=()=>p.evaluate(()=>window.livlivPoster);
let restored=false;
try{
 for(const kind of ['baseline','progressive']){
  const name='jpeg-'+kind,bytes=await fs.readFile(root+'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/poster-'+kind+'.jpg');
  await reset();await load(name,'data:image/jpeg;base64,'+bytes.toString('base64'));
  await wait(()=>window.livlivPoster?.showing&&!window.livlivPoster.ready);
  const state=await read();
  if(state.error||state.scale!==1||state.textureWidth!==32||state.textureHeight!==48||state.topRed<245||state.topBlue>10||state.bottomBlue<245||state.bottomRed>10)throw Error('JPEG pixels failed '+JSON.stringify(state));
  if(Math.abs(state.width/state.height-2/3)>.01)throw Error('JPEG contain ratio failed');
  results.states[name]=state;
  await release(name);
  await wait(()=>window.livlivPoster.ready&&!window.livlivPoster.showing&&window.livlivPoster.textures===0);
  results.states[name+'-released']=await read();
 }
 await reset();await load('first','/poster/avatar.webp');
 await wait(()=>window.livlivPoster?.showing&&!window.livlivPoster.ready);
 results.states.first=await read();
 if(results.states.first.width>results.states.first.boundsWidth+.5||results.states.first.height>results.states.first.boundsHeight+.5)throw Error('Poster overflows bounds');
 console.log(await p.screenshot({path:root+'unity/TestResults/webgl-poster-visible.png'}));
 await release('first');
 await wait(()=>window.livlivPosterLoads.first.state==='ready'&&window.livlivPoster.ready&&!window.livlivPoster.showing&&!window.livlivPoster.url&&window.livlivPoster.textures===0);
 results.states.firstFrame=await read();

 // A loaded character stays; a replacement's poster is never requested/displayed.
 await load('existing','/poster/avatar.webp?existing',false);
 await wait(()=>window.livlivPosterLoads.existing?.state==='failed');results.states.existing=await read();
 if(!results.states.existing.ready||results.states.existing.showing||results.states.existing.url)throw Error('Existing character lost');
 const counts=await p.evaluate(()=>fetch('/counts').then(r=>r.json()));if(counts['/poster/avatar.webp?existing'])throw Error('Existing model fetched a poster');

 // Old image download is held; newer image and model take over.
 await reset();await load('old','/poster/avatar.webp?id='+id('old-image'));
 await wait(()=>window.livlivPoster.loading);
 await load('new','/poster/avatar.webp?new');
 await wait(()=>window.livlivPoster.showing&&window.livlivPoster.url==='/poster/avatar.webp?new'&&window.livlivPosterLoads.old.state==='cancelled');
 await release('old-image');await release('old');
 results.states.replaced=await read();
 if(results.states.replaced.url!=='/poster/avatar.webp?new')throw Error('Late poster replaced current one');
 await release('new');await wait(()=>window.livlivPosterLoads.new.state==='ready'&&!window.livlivPoster.showing&&window.livlivPoster.textures===0);
 results.states.replacementFrame=await read();

 await reset();await load('cancel','/poster/avatar.webp');await wait(()=>window.livlivPoster.showing);
 await send('CancelPosterModel');await wait(()=>window.livlivPosterLoads.cancel.state==='cancelled'&&!window.livlivPoster.showing&&window.livlivPoster.textures===0);
 results.states.cancel=await read();await release('cancel');

 await reset();await load('invalid','/poster/invalid');await wait(()=>!!window.livlivPoster.error);
 results.states.invalidPoster=await read();
 if(results.states.invalidPoster.showing||results.states.invalidPoster.ready)throw Error('Bad poster did not settle independently');
 await release('invalid');await wait(()=>window.livlivPosterLoads.invalid.state==='ready'&&window.livlivPoster.ready&&!window.livlivPoster.showing);
 results.states.modelAfterBadPoster=await read();

 await send('EndPosterProbe');await wait(()=>window.livlivPoster.ready&&!window.livlivPoster.showing&&!window.livlivPoster.loading);
 results.states.restored=await read();restored=true;
 results.sourceHashes={};
 for(const name of ['src/base.ts','unity/Packages/ai.livliv.player/Runtime/base.cs','unity/Packages/ai.livliv.player/Runtime/loading.cs','unity/Packages/ai.livliv.player/Runtime/Platform/Unity/Player.cs','unity/Packages/ai.livliv.ugui/Runtime/PosterPresenter.cs','unity/Assets/Verification/PortVerification.Poster.cs','unity/Assets/Verification/VerificationHud.cs'])results.sourceHashes[name]=crypto.createHash('sha256').update(await fs.readFile(root+name)).digest('hex');
 await fs.writeFile(root+'unity/TestResults/webgl-poster.json',JSON.stringify(results,null,2)+'\n');console.log(results);
}finally{if(!restored)await send('EndPosterProbe');}
