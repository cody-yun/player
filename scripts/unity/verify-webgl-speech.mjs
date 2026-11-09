// Run through ego-browser nodejs with absolute `root` and an authorized `speechSpaceId`.
// Does not create/reclaim a TaskSpace. No camera/microphone is used.
const fs = await import('node:fs/promises'), crypto = await import('node:crypto');
const p = (await taskSpace(speechSpaceId)).page('p1');
await p.goto('http://127.0.0.1:8772/');
await p.waitForFunction(() => window.livlivVerification?.frames > 15, undefined, {timeout: 45000});
const evidence = {at: new Date().toISOString(), browser: await p.evaluate(() => navigator.userAgent), cases: {}};
await p.evaluate(() => {
  const button = document.createElement('button'); button.id = 'speech-probe-run'; button.textContent = 'Run speech verification';
  Object.assign(button.style, {position: 'fixed', top: '4px', left: '4px', zIndex: 9999});
  button.onclick = () => window.livlivUnity.SendMessage('LivLivVerification', 'StartSpeechProbe', JSON.stringify(window.speechProbeRequest));
  document.body.append(button);
});
console.log(await p.snapshot());
const start = async request => {
  await p.evaluate(request => { window.speechProbeRequest = request; }, request);
  await p.click('#speech-probe-run', {label: 'Testing speech playback'});
  await p.waitForFunction(id => window.livlivSpeechVerification?.id === id, request.id, {timeout: 10000});
};
const read = () => p.evaluate(() => window.livlivSpeechVerification);
const finished = async id => {
  await p.waitForFunction(id => { const s = window.livlivSpeechVerification; return s?.id === id && !s.busy && s.idle > 0; }, id, {timeout: 15000});
  return read();
};
const verifyPlayback = result => {
  if (result.errors || result.peak <= 1e-5 || result.maximumMouth <= .1 || result.scale !== 1 || result.idle !== 1)
    throw Error('Playback failed: ' + JSON.stringify(result));
};
const dispose = async () => {
  await p.evaluate(() => { window.speechProbeContexts = Object.values(window.livlivSpeechDebug.outputs).map(o => o.ctx).filter(Boolean); window.livlivUnity.SendMessage('LivLivVerification', 'DisposeSpeechProbe'); });
  await p.waitForFunction(() => { const s = window.livlivSpeechVerification; return s.disposed && !s.busy && !s.pieces && !s.loads && !s.decodes && !s.contexts.length && window.speechProbeContexts.every(c => c.state === 'closed'); }, undefined, {timeout: 10000});
};
try {
  await start({id: 'formats', urls: ['/speech/line.wav', '/speech/line.mp3', '/speech/float.wav']});
  evidence.cases.formats = await finished('formats'); verifyPlayback(evidence.cases.formats); await dispose();

  const bytes = (await fs.readFile(root + 'unity/Packages/ai.livliv.player/Tests/Graphics/Fixtures/speech-stereo.mp3.bytes')).toString('base64');
  await start({id: 'bytes', bytes}); evidence.cases.bytes = await finished('bytes'); verifyPlayback(evidence.cases.bytes); await dispose();

  const streamId = 'stream-' + Date.now();
  await start({id: 'stream', urls: ['/speech/slow.wav?id=' + streamId]});
  await p.waitForFunction(() => { const s = window.livlivSpeechVerification; return s.id === 'stream' && s.busy && s.peak > 1e-5 && s.maximumMouth > .1; }, undefined, {timeout: 10000});
  const state = await p.evaluate(async () => await (await fetch('/speech/state')).json());
  if (!state[streamId]?.prefix || state[streamId].completed) throw Error('Playback was not observed before EOF');
  evidence.cases.streamingPrefix = {player: await read(), server: state[streamId]};
  await p.evaluate(id => fetch('/speech/release?id=' + id).then(r => r.text()), streamId);
  evidence.cases.stream = await finished('stream'); verifyPlayback(evidence.cases.stream); await dispose();

  const cancelId = 'cancel-' + Date.now();
  await start({id: 'cancel', urls: ['/speech/slow.wav?id=' + cancelId]});
  await p.waitForFunction(() => window.livlivSpeechVerification.id === 'cancel' && window.livlivSpeechVerification.pieces > 0, undefined, {timeout: 10000});
  await p.evaluate(() => { window.cancelledSpeechLoads = Object.values(window.livlivSpeechDebug.loads); window.livlivUnity.SendMessage('LivLivVerification', 'StopSpeechProbe'); });
  await p.waitForFunction(() => { const s = window.livlivSpeechVerification; return s.stopped && !s.busy && !s.loads && !s.pieces && window.cancelledSpeechLoads.every(l => l.abort.signal.aborted); }, undefined, {timeout: 10000});
  evidence.cases.cancel = await read();
  await p.evaluate(id => fetch('/speech/release?id=' + id).then(r => r.text()), cancelId); await dispose();

  for (const [id, url] of [['missing', '/speech/absent.mp3'], ['invalid', '/speech/invalid']]) {
    await start({id, urls: [url]}); const result = await finished(id); evidence.cases[id] = result;
    if (result.errors !== 1 || result.busy || result.pieces) throw Error('Failure did not settle: ' + JSON.stringify(result));
    await dispose();
  }
  await start({id: 'restart', urls: ['/speech/line.mp3']}); evidence.cases.restart = await finished('restart'); verifyPlayback(evidence.cases.restart); await dispose();
  evidence.final = await read();
  evidence.sourceHashes = {};
  for (const name of ['unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivSpeech.jslib', 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLSpeech.cs', 'unity/Packages/ai.livliv.player/Runtime/runtime/speech.cs', 'unity/Packages/ai.livliv.player/Runtime/Platform/Audio/SpeechWavReader.cs', 'unity/Assets/Verification/PortVerification.WebSpeech.cs', 'unity/Assets/Verification/Probe.jslib'])
    evidence.sourceHashes[name] = crypto.createHash('sha256').update(await fs.readFile(root + name)).digest('hex');
  await fs.writeFile(root + 'unity/TestResults/webgl-speech.json', JSON.stringify(evidence, null, 2) + '\n');
  console.log({cases: Object.keys(evidence.cases), final: evidence.final});
} finally {
  await p.evaluate(() => {
    window.livlivUnity.SendMessage('LivLivVerification', 'DisposeSpeechProbe');
    document.querySelector('#speech-probe-run')?.remove();
    delete window.speechProbeRequest; delete window.speechProbeContexts; delete window.cancelledSpeechLoads;
  });
}
