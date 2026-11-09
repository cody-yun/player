// Observe onState on the unmodified source; a deterministic AudioContext event boundary.
import fs from 'node:fs';
import crypto from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { SpeechPlayer } from '../../src/runtime/speech.ts';
const root = fileURLToPath(new URL('../../', import.meta.url));
let context, initialState;
class Context {
  constructor() { this.state = initialState; this.destination = {}; context = this; }
  createGain() { return { gain: { value: 1 }, connect() {} }; }
  createBuffer() { return {}; }
  createBufferSource() { return { connect() {}, start() {} }; }
  async resume() {} // Simulate autoplay still blocked until the explicit state event.
  async close() { this.change('closed'); }
  change(value) { if (this.state === value) return; this.state = value; queueMicrotask(() => this.onstatechange?.()); }
}
globalThis.window = { AudioContext: Context };
Object.defineProperty(globalThis, 'navigator', { value: {}, configurable: true });
const scenarios = [];
for (const [name, initial, operations] of [
  ['blocked-resume-interrupt-close', 'suspended', ['created', 'unlock', 'running', 'unlock', 'suspended', 'interrupted', 'running', 'stop', 'dispose', 'dispose']],
  ['already-running', 'running', ['created', 'unlock', 'stop', 'dispose']],
  ['never-unlocked', 'suspended', ['created', 'dispose']]
]) {
  initialState = initial; context = null;
  const speech = new SpeechPlayer(), events = [], states = [];
  speech.onState = () => events.push(!!speech.running);
  for (const operation of operations) {
    if (operation === 'unlock') speech.unlock();
    else if (operation === 'stop') speech.stop();
    else if (operation === 'dispose') speech.dispose();
    else if (operation !== 'created') context.change(operation);
    await Promise.resolve(); await Promise.resolve();
    states.push({ operation, running: !!speech.running, busy: speech.busy, events: [...events] });
  }
  scenarios.push({ name, initial, states });
}
const source = 'src/runtime/speech.ts';
const output = JSON.stringify({ sourceHashes: { [source]: crypto.createHash('sha256').update(fs.readFileSync(root + source)).digest('hex') }, scenarios }) + '\n';
const destination = root + 'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech-state.json';
if (process.argv.includes('--check')) {
  if (fs.readFileSync(destination, 'utf8') !== output) throw Error('Speech state fixture differs');
  console.log('Speech state fixture matches original');
} else { fs.writeFileSync(destination, output); console.log('3 original state scenarios / 16 observations'); }
