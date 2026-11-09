// Offline rehearsal of a known upstream WAV fix. Only a temporary directory and TestResults are written.
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import assert from 'node:assert/strict'
import zlib from 'node:zlib'
import crypto from 'node:crypto'
import { execFileSync, spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
const root = fileURLToPath(new URL('../../', import.meta.url))
const output = path.join(root, 'unity/TestResults/upstream-rehearsal')
fs.mkdirSync(output, { recursive: true })
const oldCommit = '42a483ae9205e342bda91d063601ce91d4319f84'
const fixedCommit = '98eeda1b942706a7b8b97bd3169f961943ac0a2a'
const baseline = JSON.parse(fs.readFileSync(path.join(root, 'unity/Packages/ai.livliv.player/compatibility.json'))).commit
const git = (...args) => execFileSync('git', args, { cwd: root })
const sha = bytes => crypto.createHash('sha256').update(bytes).digest('hex')
const generator = 'scripts/unity/generate-speech-wav.mjs'
const fixture = 'unity/Packages/ai.livliv.player/Tests/Editor/Fixtures/speech-wav.json.gz'
const source = 'src/runtime/speech.ts'
const protectedPaths = ['unity/Packages/ai.livliv.player/compatibility.json', generator, fixture, source,
  'unity/Packages/ai.livliv.player/Runtime/runtime/speech.cs', 'unity/Packages/ai.livliv.player/Runtime/Platform/Audio/SpeechWavReader.cs']
const protectedHashes = () => Object.fromEntries(protectedPaths.map(p => [p, sha(fs.readFileSync(path.join(root, p)))]))
const hashesBefore = protectedHashes(), gitBefore = git('status', '--porcelain=v1', '-uall').toString()
const temporary = fs.mkdtempSync(path.join(os.tmpdir(), 'livliv-upstream-rehearsal-'))
try {
  const audit = ref => {
    const result = spawnSync(process.execPath, [path.join(root, 'scripts/unity/check-upstream.mjs'), ...ref ? ['--ref', ref] : []], { cwd: root, encoding: 'utf8' })
    assert.ifError(result.error)
    const report = JSON.parse(result.stdout)
    return { result, report }
  }
  const historical = audit(oldCommit), current = audit()
  assert.equal(historical.result.status, 1); assert.equal(current.result.status, 0)
  // A newer baseline may contain unrelated fixes as well as the WAV fix. Keep the historical
  // rehearsal useful without pretending those differences disappeared or advancing hashes.
  const changedSources = git('diff', '--name-only', baseline, oldCommit, '--', 'src/').toString().trim().split('\n').filter(p => p.endsWith('.ts')).sort()
  assert.deepEqual(historical.report.sourceChanges.map(c => c.source).sort(), changedSources)
  assert.ok(changedSources.includes(source))
  assert.ok(historical.report.impactedModules.some(m => m.source === 'src/voice.ts'))
  const reviewedFixtures = historical.report.fixtureReview.map(f => path.basename(f.file));
  for (const required of ['drivers.json.gz', 'speech-wav.json.gz', 'speech.json.gz']) assert.ok(reviewedFixtures.includes(required));
  assert.equal(historical.report.graphNotes.length, 0)
  assert.equal(current.report.issues.length, 0)
  fs.writeFileSync(path.join(output, 'historical.json'), historical.result.stdout)
  fs.writeFileSync(path.join(output, 'current.json'), current.result.stdout)
  const write = (p, bytes) => { const dest = path.join(temporary, p); fs.mkdirSync(path.dirname(dest), { recursive: true }); fs.writeFileSync(dest, bytes) }
  write(generator, fs.readFileSync(path.join(root, generator)))
  write(fixture, fs.readFileSync(path.join(root, fixture)))
  const originalFixture = JSON.parse(zlib.gunzipSync(fs.readFileSync(path.join(root, fixture))))
  write(source, git('show', `${oldCommit}:${source}`))
  const run = (check, label) => {
    const r = spawnSync(process.execPath, [path.join(temporary, generator), ...check ? ['--check'] : []], { cwd: temporary, encoding: 'utf8', timeout: 30000 })
    assert.ifError(r.error); fs.writeFileSync(path.join(output, label + '.log'), r.stdout + r.stderr); return r
  }
  const oldCheck = run(true, 'old-fixture-check')
  assert.equal(oldCheck.status, 1); assert.match(oldCheck.stderr, /WAV fixture differs/)
  assert.equal(run(false, 'old-fixture-generation').status, 0)
  const oldFixture = JSON.parse(zlib.gunzipSync(fs.readFileSync(path.join(temporary, fixture))))
  // Compare outputs, not just provenance hashes: PCM bytes/fallback remain the same, 12 rates differ.
  const withoutRate = cases => cases.map(({ rate, ...rest }) => rest)
  assert.deepEqual(withoutRate(oldFixture.cases), withoutRate(originalFixture.cases))
  const regressions = oldFixture.cases.flatMap((row, i) => row.rate === originalFixture.cases[i].rate ? [] : [{
    name: row.name, beforeRate: row.rate, expectedRate: originalFixture.cases[i].rate,
    sampleCount: row.samples.length, beforeDurationMs: 1000 * row.samples.length / row.rate,
    expectedDurationMs: 1000 * row.samples.length / originalFixture.cases[i].rate }])
  assert.equal(regressions.length, 12)
  write(fixture, fs.readFileSync(path.join(root, fixture)))
  write(source, git('show', `${fixedCommit}:${source}`))
  const fixedCheck = run(true, 'fixed-fixture-check'); assert.equal(fixedCheck.status, 0)
  const hashesAfter = protectedHashes()
  assert.deepEqual(hashesAfter, hashesBefore)
  assert.equal(git('status', '--porcelain=v1', '-uall').toString(), gitBefore)
  const report = { result: 'Passed', baseline, historicalCandidate: oldCommit, fixedCommit,
    currentAuditExit: current.result.status, historicalAuditExit: historical.result.status,
    oldFixtureCheckExit: oldCheck.status, fixedFixtureCheckExit: fixedCheck.status,
    cases: originalFixture.cases.length, reviewedFixtures, historicalSourceChanges: changedSources,
    historicalImpactedModules: historical.report.impactedModules.map(m => m.source), regressions, protectedHashes: hashesAfter,
    scope: 'Known local Git history only; no fetch/merge/baseline update. C# and Unity tests are separate evidence.' }
  fs.writeFileSync(path.join(output, 'evidence.json'), JSON.stringify(report, null, 2) + '\n')
  console.log(JSON.stringify({ result: report.result, cases: report.cases, differingSampleRates: regressions.length, output }, null, 2))
} finally { fs.rmSync(temporary, { recursive: true, force: true }) }
