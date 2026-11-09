// Execute the pinned source without edits. Web Audio is a deterministic clock/scheduler
// double here; these fixtures verify arithmetic and scheduling, not a physical speaker.
import fs from 'node:fs';
import zlib from 'node:zlib';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {SpeechPlayer, envelope} from '../../src/runtime/speech.ts';

const root = fileURLToPath(new URL('../../', import.meta.url));
const signal = (rate, n, offset = 0) => Float32Array.from({length: n}, (_, i) => {
  const t = (i + offset) / rate;
  const syllable = .04 + .8 * Math.max(0, Math.sin(t * 31)) ** 2;
  return syllable * (.5 * Math.sin(t * 2 * Math.PI * 270) + .25 * Math.sin(t * 2 * Math.PI * 870) + .1 * Math.sin(t * 2 * Math.PI * 2400));
});
const envelopes = [];
for (const rate of [8000, 16000, 22050, 24000, 44100, 48000]) {
  for (const kind of ['empty', 'silence', 'short', 'speech']) {
    const x = kind === 'empty' ? new Float32Array() : kind === 'silence' ? new Float32Array(1000) : signal(rate, kind === 'short' ? 17 : Math.floor(rate * .67));
    envelopes.push({rate, kind, samples: Array.from(x), expected: Array.from(envelope(x, rate))});
  }
}
let time = 0, id = 0;
const scheduled = [];
class Context {
  state = 'running'; sampleRate = 24000; destination = {};
  get currentTime() { return time; }
  outputLatency = 0; baseLatency = 0;
  getOutputTimestamp() { return {contextTime: time, performanceTime: time * 1000}; }
  createGain() { return {gain: {value: 1}, connect() {}}; }
  createBuffer(channels, n, rate) { const data = new Float32Array(n); return {getChannelData: () => data, duration: n / rate, sampleRate: rate}; }
  createBufferSource() {
    return {connect() {}, stop() {}, start(at) { scheduled.push({at, n: this.buffer.getChannelData(0).length, rate: this.buffer.sampleRate}); }};
  }
  async close() {}
}
globalThis.window = {AudioContext: Context, setTimeout: () => ++id, setInterval: () => ++id};
Object.defineProperty(performance, 'now', {value: () => time * 1000});
const streams = [];
for (const rate of [16000, 24000, 44100]) {
  time = 0; scheduled.length = 0;
  const speech = new SpeechPlayer(); speech.unlock();
  let clip = null, offset = 0;
  const operations = [];
  for (const [duration, arrived, complete] of [[.18, 0, false], [.13, .09, false], [.27, .49, true]]) {
    time = arrived;
    const x = signal(rate, Math.floor(duration * rate), offset); offset += x.length;
    clip = speech.play(clip, x, rate);
    if (complete) { clip.complete = true; speech.update(clip); }
    const reads = [];
    for (const dt of [0, .04, .057, .10]) {
      time = arrived + dt;
      reads.push({time, mouth: speech.read(), vowel: speech.vowel()});
    }
    operations.push({time: arrived, samples: Array.from(x), complete, reads});
  }
  streams.push({rate, operations, scheduled: [...scheduled]});
  speech.dispose();
}
const sourceHashes = {};
for (const file of ['src/runtime/speech.ts', 'src/runtime/vowel.ts']) sourceHashes[file] = crypto.createHash('sha256').update(fs.readFileSync(root + file)).digest('hex');
const text = JSON.stringify({sourceHashes, envelopes, streams}) + '\n';
const dest = root + 'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech.json.gz';
if (process.argv.includes('--check')) {
  if (zlib.gunzipSync(fs.readFileSync(dest)).toString() !== text) throw Error('speech fixture differs');
  console.log('speech fixture matches upstream');
} else {
  fs.writeFileSync(dest, zlib.gzipSync(text));
  console.log(`${envelopes.length} envelope cases, ${streams.length} streams / 36 timed reads`);
}
