// ego-browser nodejs; inject absolute root and the already-authorized speechSpaceId.
// Real AudioContext suspend/resume/close; no audio clip or capture devices.
const fs = await import('node:fs/promises'), crypto = await import('node:crypto');
const p = (await taskSpace(speechSpaceId)).page('p1');
await p.goto('http://127.0.0.1:8772/?speech-state=' + Date.now());
await p.waitForFunction(() => window.livlivVerification?.frames > 15, undefined, {timeout: 45000});
await p.evaluate(() => {
  const button = document.createElement('button'); button.id = 'speech-state-run'; button.textContent = 'Verify audio state';
  Object.assign(button.style, {position: 'fixed', top: '4px', left: '4px', zIndex: 9999});
  button.onclick = () => window.livlivUnity.SendMessage('LivLivVerification', window.speechStateAction === 'resume' ? 'UnlockSpeechProbe' : 'StartSpeechProbe',
    window.speechStateAction === 'resume' ? '' : JSON.stringify({id: window.speechStateId, urls: []}));
  document.body.append(button);
});
console.log(await p.snapshot());
const report = {browser: await p.evaluate(() => navigator.userAgent), states: []};
const read = () => p.evaluate(() => window.livlivSpeechVerification);
const record = async name => { const state = await read(); if (state.errors || state.scale !== 1) throw Error(JSON.stringify(state)); report.states.push({name, ...state}); return state; };
const begin = async id => {
  await p.evaluate(id => { window.speechStateId = id; window.speechStateAction = 'start'; }, id);
  await p.click('#speech-state-run', {label: 'Starting audio state verification'});
  await p.waitForFunction(id => {
    const s = window.livlivSpeechVerification;
    return s?.id === id && typeof s.stateChanges === 'number' && s.contexts.length === 1 && s.contexts[0].state === 'running';
  }, id, {timeout: 10000});
  await p.evaluate(() => {
    const context = Object.values(window.livlivSpeechDebug.outputs)[0].ctx, events = [];
    window.speechStateContext = context; window.speechStateBrowserEvents = events;
    context.addEventListener('statechange', () => events.push(context.state));
  });
  // Drain any creation/resume event before measuring explicit transitions.
  const frame = (await read()).frames;
  await p.waitForFunction(frame => window.livlivSpeechVerification.frames >= frame + 3, frame);
};
try {
  await begin('state-live'); let previous = await record('ready');
  await p.evaluate(() => window.speechStateContext.suspend());
  await p.waitForFunction(count => { const s = window.livlivSpeechVerification; return s.stateChanges === count + 1 && !s.running; }, previous.stateChanges);
  previous = await record('suspended');
  await p.evaluate(() => { window.speechStateAction = 'resume'; });
  await p.click('#speech-state-run', {label: 'Resuming audio context'});
  await p.waitForFunction(count => { const s = window.livlivSpeechVerification; return s.stateChanges === count + 1 && s.running; }, previous.stateChanges);
  previous = await record('resumed');
  await p.evaluate(() => window.livlivUnity.SendMessage('LivLivVerification', 'StopSpeechProbe'));
  await p.waitForFunction(frame => window.livlivSpeechVerification.frames >= frame + 3, previous.frames);
  const stopped = await record('stopped'); if (stopped.stateChanges !== previous.stateChanges) throw Error('Stop emitted a context event');
  await p.evaluate(() => window.livlivUnity.SendMessage('LivLivVerification', 'DisposeSpeechProbe'));
  await p.waitForFunction(count => { const s = window.livlivSpeechVerification; return s.disposed && !s.running && s.stateChanges === count + 1 && !s.contexts.length && window.speechStateContext.state === 'closed'; }, stopped.stateChanges);
  const disposed = await record('disposed');
  await p.evaluate(() => window.livlivUnity.SendMessage('LivLivVerification', 'DisposeSpeechProbe'));
  if ((await record('disposed-again')).stateChanges !== disposed.stateChanges) throw Error('Repeated dispose emitted a duplicate event');
  report.ownedContextEvents = await p.evaluate(() => window.speechStateBrowserEvents);
  await begin('state-external-close'); previous = await record('second-ready');
  await p.evaluate(() => window.speechStateContext.close());
  await p.waitForFunction(count => { const s = window.livlivSpeechVerification; return s.stateChanges === count + 1 && !s.running; }, previous.stateChanges);
  previous = await record('context-closed');
  await p.evaluate(() => window.livlivUnity.SendMessage('LivLivVerification', 'DisposeSpeechProbe'));
  await p.waitForFunction(() => window.livlivSpeechVerification.disposed && !window.livlivSpeechVerification.contexts.length);
  if ((await record('closed-context-disposed')).stateChanges !== previous.stateChanges) throw Error('Already-closed context reported a second close');
  report.externalContextEvents = await p.evaluate(() => window.speechStateBrowserEvents);
  report.sourceHashes = {};
  for (const name of ['unity/Packages/ai.livliv.player/Runtime/runtime/speech.cs', 'unity/Packages/ai.livliv.player/Runtime/Platform/Audio/SpeechContracts.cs',
    'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/WebGLSpeech.cs', 'unity/Packages/ai.livliv.player/Runtime/Platform/Unity/UnitySpeechOutput.cs',
    'unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivSpeech.jslib', 'unity/Assets/Verification/PortVerification.WebSpeech.cs'])
    report.sourceHashes[name] = crypto.createHash('sha256').update(await fs.readFile(root + name)).digest('hex');
  report.buildHashes = {};
  for (const name of await fs.readdir(root + 'unity/TestResults/WebGL/Build'))
    report.buildHashes[name] = crypto.createHash('sha256').update(await fs.readFile(root + 'unity/TestResults/WebGL/Build/' + name)).digest('hex');
  report.result = 'Passed';
} finally {
  await p.evaluate(() => { window.livlivUnity.SendMessage('LivLivVerification', 'DisposeSpeechProbe'); document.getElementById('speech-state-run')?.remove(); });
}
report.final = await p.evaluate(() => ({scale: window.livlivVerification.scale, microphone: window.livlivVerification.microphone,
  tracking: window.livlivVerification.tracking, width: innerWidth, height: innerHeight,
  overflow: document.documentElement.scrollWidth > innerWidth || document.documentElement.scrollHeight > innerHeight}));
if (report.final.overflow || report.final.scale !== 1 || report.final.microphone || report.final.tracking) throw Error('Final sample state differs');
await fs.writeFile(root + 'unity/TestResults/webgl-speech-state.json', JSON.stringify(report, null, 2));
console.log(JSON.stringify({result: report.result, states: report.states.map(s => [s.name, s.stateChanges, s.running]), final: report.final}));
console.log(await p.snapshot());
