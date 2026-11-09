// Run through ego-browser with injected absolute root. Offline contexts never open a microphone or speaker.
const fs=await import('node:fs/promises'),zlib=await import('node:zlib'),crypto=await import('node:crypto');
const t=await taskSpace(3),p=t.page('p1');await p.goto('http://127.0.0.1:8769/');
const cases=await p.evaluate(async()=>{
 const rows=[];
 for(const rate of [16000,44100,48000,96000]){
  const length=8192,ctx=new OfflineAudioContext(1,length,rate),buffer=ctx.createBuffer(1,length,rate),pcm=buffer.getChannelData(0);let seed=19;
  for(let i=0;i<length;i++){seed=(Math.imul(seed,1664525)+1013904223)>>>0;pcm[i]=(.1*Math.sin(2*Math.PI*310*i/rate)+.17*Math.sin(2*Math.PI*1700*i/rate)+.05*Math.sin(2*Math.PI*5100*i/rate)+(seed/4294967296-.5)*.02)*(i<4000?1:.4);}
  const src=ctx.createBufferSource();src.buffer=buffer;
  const band=ctx.createBiquadFilter();band.type='bandpass';band.frequency.value=650;band.Q.value=.55;
  const an=ctx.createAnalyser();an.fftSize=1024;const spec=ctx.createAnalyser();spec.fftSize=1024;spec.smoothingTimeConstant=0;
  src.connect(band);band.connect(an);an.connect(ctx.destination);src.connect(spec);const silent=ctx.createGain();silent.gain.value=0;spec.connect(silent);silent.connect(ctx.destination);
  src.start();const rendered=await ctx.startRendering();const samples=new Float32Array(1024),raw=new Float32Array(1024),db=new Float32Array(512);an.getFloatTimeDomainData(samples);spec.getFloatTimeDomainData(raw);spec.getFloatFrequencyData(db);
  rows.push({rate,input:Array.from(pcm),filtered:Array.from(rendered.getChannelData(0)),time:Array.from(samples),raw:Array.from(raw),power:Array.from(db,d=>Math.fround(10**(d/10)))});
 }
 return {browser:navigator.userAgent,cases:rows};
});
const sourceHashes={};for(const path of ['src/runtime/voice.ts','src/runtime/vowel.ts'])sourceHashes[path]=crypto.createHash('sha256').update(await fs.readFile(root+path)).digest('hex');
await fs.writeFile(root+'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/audio.json.gz',zlib.gzipSync(JSON.stringify({sourceHashes,...cases})));
console.log(cases.cases.map(r=>({rate:r.rate,samples:r.input.length,peak:Math.max(...r.power)})));
