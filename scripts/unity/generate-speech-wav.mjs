import fs from 'node:fs';
import zlib from 'node:zlib';
import crypto from 'node:crypto';
import {stripTypeScriptTypes} from 'node:module';
import {fileURLToPath} from 'node:url';
const root = fileURLToPath(new URL('../../', import.meta.url));
const source = fs.readFileSync(root + 'src/runtime/speech.ts', 'utf8');
// Execute the actual private Incoming and WAV helper bodies without editing upstream files.
const segment = source.slice(source.indexOf('class Incoming {'), source.indexOf('/** samples that grow'));
const {Incoming} = new Function('RATE', stripTypeScriptTypes(segment) + '\nreturn {Incoming};')(24000);
const tag = s => Buffer.from(s, 'ascii');
const chunk = (id, data) => {
  const size = Buffer.alloc(4); size.writeUInt32LE(data.length);
  return Buffer.concat([tag(id), size, data, ...(data.length % 2 ? [Buffer.alloc(1)] : [])]);
};
function wave(rate, channels, extra = false, encoding = 1, bits = 16) {
  const fmt = Buffer.alloc(extra ? 18 : 16);
  fmt.writeUInt16LE(encoding); fmt.writeUInt16LE(channels, 2); fmt.writeUInt32LE(rate, 4);
  fmt.writeUInt32LE(rate * channels * 2, 8); fmt.writeUInt16LE(channels * 2, 12); fmt.writeUInt16LE(bits, 14);
  const pcm = Buffer.alloc(53 * channels * 2);
  for (let i = 0; i < 53; i++) for (let c = 0; c < channels; c++) pcm.writeInt16LE(((i * 1447 + c * 1973) % 65536) - 32768, (i * channels + c) * 2);
  const data = chunk('data', pcm); data.writeUInt32LE(0x7fffffff, 4); // streaming sentinel
  const body = Buffer.concat([tag('WAVE'), ...(extra ? [chunk('JUNK', Buffer.from([4, 7, 9]))] : []), chunk('fmt ', fmt), data]);
  const size = Buffer.alloc(4); size.writeUInt32LE(body.length);
  return Buffer.concat([tag('RIFF'), size, body]);
}
const cases = [];
async function run(name, bytes, sizes) {
  const parts = []; for (let at = 0, i = 0; at < bytes.length; i++) { const n = Math.min(sizes[i % sizes.length], bytes.length - at); parts.push(bytes.subarray(at, at + n)); at += n; }
  let index = 0, fallback = null;
  const context = {decodeAudioData: async b => { fallback = Buffer.from(b).toString('base64'); return {sampleRate: 48000, duration: 0, getChannelData: () => new Float32Array()}; }};
  globalThis.fetch = async () => ({ok: true, headers: {get: () => 'audio/wav'}, body: {getReader: () => ({read: async () => index < parts.length ? {value: parts[index++], done: false} : {done: true}})}});
  const incoming = new Incoming('fixture', context, new AbortController().signal), samples = [];
  for (let x; (x = await incoming.next());) samples.push(...x);
  cases.push({name, bytes: bytes.toString('base64'), sizes, rate: incoming.rate, samples, fallback});
}
for (const rate of [8000, 16000, 24000, 44100, 48000])
  for (const channels of [1, 2, 3])
    await run(`${rate}/${channels} split`, wave(rate, channels, true), [1, 2, 3, 7, 11]);
await run('one delivery', wave(24000, 2), [99999]);
await run('partial last frame', wave(24000, 2).subarray(0, -1), [13, 1, 7]);
await run('float format fallback', wave(24000, 1, false, 3, 32), [1]);
await run('non-wave fallback', Buffer.from('not a wav file with arbitrary binary data'), [3]);
await run('truncated header fallback', wave(24000, 1).subarray(0, 31), [2]);
await run('invalid rate uses voice default', wave(0, 1), [1, 18, 7]);
const sourceHashes = {'src/runtime/speech.ts': crypto.createHash('sha256').update(source).digest('hex')};
const text = JSON.stringify({sourceHashes, cases}) + '\n';
const dest = root + 'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech-wav.json.gz';
if (process.argv.includes('--check')) {
  if (zlib.gunzipSync(fs.readFileSync(dest)).toString() !== text) throw Error('WAV fixture differs');
  console.log('WAV fixture matches original Incoming');
} else { fs.writeFileSync(dest, zlib.gzipSync(text)); console.log(cases.length + ' original Incoming WAV cases'); }
