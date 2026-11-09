import fs from 'node:fs';
import vm from 'node:vm';
import test from 'node:test';
import assert from 'node:assert/strict';
const plugin = fs.readFileSync(new URL('../../unity/Packages/ai.livliv.player/Runtime/Plugins/WebGL/LivLivSpeech.jslib', import.meta.url), 'utf8');
const flush = async () => { for (let i = 0; i < 20; i++) await Promise.resolve(); };
function setup({suspended = false, webkit = false, timestamp = true, noAudio = false, decodeDelayed = false, decodeFailure = false, duration = .1, fetchFailure = false, maxDecoded = 2400} = {}) {
  const log = {nodes: [], closes: 0, resumes: 0, readerCancels: 0, fetches: [], decodeBytes: null};
  let decodeResolve, readResolve;
  const input = [], decoded = {length: maxDecoded, numberOfChannels: 2, sampleRate: 24000, duration, getChannelData: channel => { assert.equal(channel, 0); return new Float32Array(maxDecoded).fill(.125); }};
  const reader = {read: () => input.length ? Promise.resolve(input.shift()) : new Promise(r => { readResolve = r; }), cancel: async () => { log.readerCancels++; }};
  class Context {
    constructor(options) { this.state = suspended ? 'suspended' : 'running'; this.currentTime = 1; this.sampleRate = 24000; this.outputLatency = .1; this.baseLatency = .02; this.destination = {}; log.options = options; log.ctx = this; }
    getOutputTimestamp() { return timestamp ? {contextTime: .8, performanceTime: 1000} : null; }
    createGain() { return {gain: {value: 1}, connect() {}, disconnect() {}}; }
    createBuffer(channels, n, rate) { assert.equal(channels, 1); const x = new Float32Array(n); return {sampleRate: rate, getChannelData: () => x}; }
    createBufferSource() {
      const n = {stops: 0, disconnects: 0, connect(to) { this.to = to; }, start(at) { this.at = at; }, stop() { this.stops++; }, disconnect() { this.disconnects++; }};
      log.nodes.push(n); return n;
    }
    resume() { log.resumes++; return Promise.resolve(); }
    close() { this.state = 'closed'; log.closes++; return Promise.resolve(); }
    decodeAudioData(bytes) { log.decodeBytes = new Uint8Array(bytes).slice(); if (decodeFailure) return Promise.reject(Error('invalid encoded audio')); return decodeDelayed ? new Promise(r => { decodeResolve = r; }) : Promise.resolve(decoded); }
  }
  const window = noAudio ? {} : webkit ? {AudioContext: Context, webkitAudioContext: Context} : {AudioContext: Context};
  const context = vm.createContext({console, Float32Array, Uint8Array, Promise, Error, Math, String, Object, URL, AbortController,
    window, navigator: {audioSession: {}}, document: {baseURI: 'https://example.test/chat/'}, performance: {now: () => 1250},
    fetch: async (url, options) => { log.fetches.push({url, options}); if (fetchFailure) throw Error('offline'); return {ok: true, headers: {get: () => 'audio/wav'}, body: {getReader: () => reader}}; },
    HEAPU8: new Uint8Array(65536), HEAPF32: new Float32Array(16384), LibraryManager: {library: {}},
    mergeInto: (a, b) => Object.assign(a, b), UTF8ToString: x => x, stringToUTF8: s => { log.error = s; }});
  vm.runInContext(plugin, context); const lib = context.LibraryManager.library; context.LivLivSpeechBrowser = lib.$LivLivSpeechBrowser;
  const id = lib.LivLivSpeech_Create();
  return {context, lib, id, log, decoded, deliver(value, done = false) { const r = {value, done}; if (readResolve) { const cb = readResolve; readResolve = null; cb(r); } else input.push(r); }, decodeResolve: () => decodeResolve(decoded)};
}
test('context is lazy, output clock uses timestamp and scheduled PCM is copied', () => {
  const s = setup(); assert.equal(s.log.ctx, undefined); s.lib.LivLivSpeech_Volume(s.id, .3);
  assert.equal(s.lib.LivLivSpeech_Unlock(s.id), 1); assert.equal(s.log.options.sampleRate, 24000);
  assert.equal(s.context.navigator.audioSession.type, 'playback'); assert.equal(s.lib.LivLivSpeech_Time(s.id, 1), 1.05);
  assert.equal(s.lib.LivLivSpeech_WallTime(), 1.25);
  s.context.HEAPF32.set([.1, .2, .3], 8);
  const p = s.lib.LivLivSpeech_Schedule(s.id, 32, 3, 16000, 1.04), node = s.log.nodes[0];
  assert.equal(node.at, 1.04); assert.equal(node.to.gain.value, .3);
  s.context.HEAPF32.fill(0); assert.equal(node.buffer.getChannelData(0)[1], Math.fround(.2));
  assert.equal(s.lib.LivLivSpeech_Ended(p), 0); node.onended(); assert.equal(s.lib.LivLivSpeech_Ended(p), 1);
  s.lib.LivLivSpeech_StopPiece(p); s.lib.LivLivSpeech_StopPiece(p); assert.equal(node.stops, 1); assert.equal(node.disconnects, 1);
  s.lib.LivLivSpeech_Dispose(s.id); s.lib.LivLivSpeech_Dispose(s.id); assert.equal(s.log.closes, 1);
});
test('WebKit uses device rate and suspended unlock primes one silent sample', () => {
  const s = setup({webkit: true, suspended: true, timestamp: false}); s.lib.LivLivSpeech_Unlock(s.id);
  assert.equal(s.log.options, undefined); assert.equal(s.log.resumes, 1); assert.equal(s.log.nodes[0].buffer.sampleRate, 22050);
  assert.equal(s.lib.LivLivSpeech_Running(s.id), 0); assert.equal(s.lib.LivLivSpeech_Time(s.id, 1), .88);
  s.log.ctx.state = 'running'; assert.equal(s.lib.LivLivSpeech_Running(s.id), 1); s.lib.LivLivSpeech_Dispose(s.id);
});
test('missing audio context reports an actionable error', () => {
  const s = setup({noAudio: true}); assert.equal(s.lib.LivLivSpeech_Unlock(s.id), 0);
  s.lib.LivLivSpeech_Error(s.id, 0, 0, 256); assert.match(s.log.error, /unavailable/); s.lib.LivLivSpeech_Dispose(s.id);
});
test('real state events are consumed once, including two non-running context states', () => {
  const s = setup({suspended: true});
  assert.equal(s.lib.LivLivSpeech_HasOpenContext(s.id), 0);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 0);
  s.lib.LivLivSpeech_Unlock(s.id);
  assert.equal(s.lib.LivLivSpeech_HasOpenContext(s.id), 1);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 0); // no invented creation event
  for (const state of ['running', 'suspended', 'interrupted']) { s.log.ctx.state = state; s.log.ctx.onstatechange(); }
  assert.equal(s.lib.LivLivSpeech_Running(s.id), 0);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 3);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 0);
  const late = s.log.ctx.onstatechange;
  s.lib.LivLivSpeech_Dispose(s.id); late();
  assert.equal(s.log.ctx.onstatechange, null);
  assert.equal(s.lib.LivLivSpeech_HasOpenContext(s.id), 0);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 0);
});
test('state delivery bounds preserve excess events for the next pump', () => {
  const s = setup(); s.lib.LivLivSpeech_Unlock(s.id);
  for (let i = 0; i < 4100; i++) s.log.ctx.onstatechange();
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 4096);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 4);
  assert.equal(s.lib.LivLivSpeech_StateChanges(s.id), 0);
  s.log.ctx.state = 'closed'; assert.equal(s.lib.LivLivSpeech_HasOpenContext(s.id), 0);
  s.lib.LivLivSpeech_Dispose(s.id);
});
test('fetch resolves relative URLs, streams before EOF, and reads fresh heaps', async () => {
  const s = setup(); const load = s.lib.LivLivSpeech_Load(s.id, '../line.wav', 100); await flush();
  assert.equal(s.log.fetches[0].url, 'https://example.test/line.wav');
  s.deliver(new Uint8Array([1, 2, 3, 4])); await flush();
  assert.equal(s.lib.LivLivSpeech_LoadState(load), 0); assert.equal(s.lib.LivLivSpeech_LoadWav(load), 1);
  assert.equal(s.lib.LivLivSpeech_LoadRead(load, 16, 2), 2); assert.deepEqual([...s.context.HEAPU8.slice(16, 18)], [1, 2]);
  s.context.HEAPU8 = new Uint8Array(131072); assert.equal(s.lib.LivLivSpeech_LoadRead(load, 24, 8), 2);
  assert.deepEqual([...s.context.HEAPU8.slice(24, 26)], [3, 4]);
  s.deliver(null, true); await flush(); assert.equal(s.lib.LivLivSpeech_LoadState(load), 1);
  s.lib.LivLivSpeech_LoadCancel(load); assert.equal(s.lib.LivLivSpeech_LoadState(load), -2);
});
test('load cancellation aborts fetch and ignores late bytes', async () => {
  const s = setup(); const load = s.lib.LivLivSpeech_Load(s.id, '/line.wav', 100); await flush();
  s.lib.LivLivSpeech_LoadCancel(load); s.deliver(new Uint8Array([1, 2])); await flush();
  assert.equal(s.log.fetches[0].options.signal.aborted, true); assert.equal(s.log.readerCancels, 1);
  assert.equal(s.lib.LivLivSpeech_LoadRead(load, 0, 16), 0); assert.equal(s.lib.LivLivSpeech_LoadState(load), -2);
});
test('byte budget preserves the already-arrived prefix and aborts the rest', async () => {
  const s = setup(); const load = s.lib.LivLivSpeech_Load(s.id, '/line.wav', 3); await flush();
  s.deliver(new Uint8Array([1, 2])); await flush(); s.deliver(new Uint8Array([3, 4])); await flush();
  assert.equal(s.lib.LivLivSpeech_LoadState(load), -1); assert.equal(s.lib.LivLivSpeech_LoadRead(load, 0, 10), 2);
  assert.equal(s.log.fetches[0].options.signal.aborted, true);
  s.lib.LivLivSpeech_Error(load, 1, 0, 256); assert.match(s.log.error, /byte limit/);
});
test('fetch failure settles without a pending stream', async () => {
  const s = setup({fetchFailure: true}); const load = s.lib.LivLivSpeech_Load(s.id, '/line.wav', 100); await flush();
  assert.equal(s.lib.LivLivSpeech_LoadState(load), -1); s.lib.LivLivSpeech_Error(load, 1, 0, 256); assert.match(s.log.error, /offline/);
});
test('HTTP failure never yields its response body as speech', async () => {
  const s = setup(); s.context.fetch = async () => ({ok: false, status: 404});
  const load = s.lib.LivLivSpeech_Load(s.id, '/missing', 100); await flush();
  assert.equal(s.lib.LivLivSpeech_LoadState(load), -1); assert.equal(s.lib.LivLivSpeech_LoadRead(load, 0, 100), 0);
  s.lib.LivLivSpeech_Error(load, 1, 0, 256); assert.match(s.log.error, /HTTP 404/);
});
test('response without readable stream uses whole decoding even for WAV MIME', async () => {
  const s = setup(); s.context.fetch = async () => ({ok: true, headers: {get: () => 'audio/wav'}, body: null, arrayBuffer: async () => new Uint8Array([1, 3, 5]).buffer});
  const load = s.lib.LivLivSpeech_Load(s.id, '/line.wav', 100); await flush();
  assert.equal(s.lib.LivLivSpeech_LoadState(load), 1); assert.equal(s.lib.LivLivSpeech_LoadWav(load), 0);
  assert.equal(s.lib.LivLivSpeech_LoadRead(load, 0, 100), 3); assert.deepEqual([...s.context.HEAPU8.slice(0, 3)], [1, 3, 5]);
});
test('cancel before fetch starts makes no request', async () => {
  const s = setup(); const load = s.lib.LivLivSpeech_Load(s.id, '/line.wav', 100);
  s.lib.LivLivSpeech_LoadCancel(load); await flush(); assert.equal(s.log.fetches.length, 0);
});
test('decoder copies encoded bytes and returns first channel across memory growth', async () => {
  const s = setup(); s.lib.LivLivSpeech_Unlock(s.id); s.context.HEAPU8.set([7, 8, 9], 32);
  const decode = s.lib.LivLivSpeech_Decode(s.id, 32, 3, 10000); s.context.HEAPU8.fill(0); await flush();
  assert.deepEqual([...s.log.decodeBytes], [7, 8, 9]); assert.equal(s.lib.LivLivSpeech_DecodeState(decode), 1);
  assert.equal(s.lib.LivLivSpeech_DecodeLength(decode), 2400);
  s.context.HEAPF32 = new Float32Array(32768); assert.equal(s.lib.LivLivSpeech_DecodeRead(decode, 128, 2400), 24000);
  assert.equal(s.context.HEAPF32[32], .125); s.lib.LivLivSpeech_DecodeCancel(decode);
});
test('short decoded clips have no PCM and decoded sample limits fail', async () => {
  const short = setup({duration: .05}); short.lib.LivLivSpeech_Unlock(short.id);
  const id = short.lib.LivLivSpeech_Decode(short.id, 0, 1, 10000); await flush(); assert.equal(short.lib.LivLivSpeech_DecodeLength(id), 0);
  const large = setup(); large.lib.LivLivSpeech_Unlock(large.id);
  const over = large.lib.LivLivSpeech_Decode(large.id, 0, 1, 100); await flush();
  assert.equal(large.lib.LivLivSpeech_DecodeState(over), -1); large.lib.LivLivSpeech_Error(over, 2, 0, 256); assert.match(large.log.error, /sample limit/);
});
test('codec errors settle and schedule failure disconnects its source', async () => {
  const s = setup({decodeFailure: true}); s.lib.LivLivSpeech_Unlock(s.id);
  const decode = s.lib.LivLivSpeech_Decode(s.id, 0, 1, 10000); await flush();
  assert.equal(s.lib.LivLivSpeech_DecodeState(decode), -1); s.lib.LivLivSpeech_Error(decode, 2, 0, 256); assert.match(s.log.error, /invalid encoded/);
  const create = s.log.ctx.createBufferSource.bind(s.log.ctx);
  s.log.ctx.createBufferSource = () => { const n = create(); n.start = () => { throw Error('schedule failed'); }; return n; };
  assert.equal(s.lib.LivLivSpeech_Schedule(s.id, 0, 10, 24000, 2), 0);
  assert.equal(s.log.nodes.at(-1).disconnects, 1); assert.equal(Object.keys(s.context.LivLivSpeechBrowser.pieces).length, 0);
});
test('output disposal retires sources, fetches and late decodes together', async () => {
  const s = setup({decodeDelayed: true}); s.lib.LivLivSpeech_Unlock(s.id);
  const p = s.lib.LivLivSpeech_Schedule(s.id, 0, 10, 24000, 2);
  const load = s.lib.LivLivSpeech_Load(s.id, '/pending.wav', 100);
  const decode = s.lib.LivLivSpeech_Decode(s.id, 0, 1, 10000); await flush();
  s.lib.LivLivSpeech_Dispose(s.id); s.decodeResolve(); s.deliver(new Uint8Array([3])); await flush();
  assert.equal(s.log.closes, 1); assert.equal(s.lib.LivLivSpeech_Ended(p), 1);
  assert.equal(s.lib.LivLivSpeech_LoadState(load), -2); assert.equal(s.lib.LivLivSpeech_DecodeState(decode), -2);
  assert.equal(Object.keys(s.context.LivLivSpeechBrowser.pieces).length, 0);
});
