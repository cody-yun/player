import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import os from 'node:os'
import path from 'node:path'
import crypto from 'node:crypto'
import { execFileSync } from 'node:child_process'
import { auditUpstream } from './upstream-audit.mjs'
const pkg = 'unity/Packages/ai.livliv.player'
const sha = b => crypto.createHash('sha256').update(b).digest('hex')
function sandbox(t) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'livliv-upstream-audit-'))
  t.after(() => fs.rmSync(root, { recursive: true, force: true }))
  const write = (name, text) => { const p = path.join(root, name); fs.mkdirSync(path.dirname(p), { recursive: true }); fs.writeFileSync(p, text) }
  const git = (...args) => execFileSync('git', args, { cwd: root, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim()
  const commit = () => { git('add', 'src', 'characters', 'API.md'); git('-c', 'user.name=Audit Fixture', '-c', 'user.email=audit@example.invalid', 'commit', '-qm', 'fixture'); return git('rev-parse', 'HEAD') }
  git('init', '-q')
  write('src/core.ts', 'export type Rate = number; export const rate = 24000;\n')
  write('src/middle.ts', "import type { Rate } from './core'; export const value: Rate = 1;\n")
  write('src/index.ts', "export { value } from './middle.js'; export const lazy = () => import('./core');\n")
  write('src/pending.ts', 'export const unused = true;\n')
  write('characters/demo/model.json', '{"name":"demo"}')
  write('API.md', '# API\n')
  const baseline = commit()
  const manifest = { commit: baseline, modules: ['core', 'middle', 'index', 'pending'].map(name => ({
    source: `src/${name}.ts`, sha256: sha(fs.readFileSync(path.join(root, `src/${name}.ts`))),
    target: `Runtime/${name}.cs`, status: name === 'pending' ? 'pending' : 'partial',
    adapters: name === 'core' ? ['Runtime/Platform/core.cs'] : [] })) }
  const saveManifest = () => write(`${pkg}/compatibility.json`, JSON.stringify(manifest))
  saveManifest()
  for (const m of manifest.modules.filter(m => m.status !== 'pending')) write(`${pkg}/${m.target}`, '// preserved port\n')
  write(`${pkg}/Runtime/Platform/core.cs`, '// preserved adapter\n')
  write(`${pkg}/Tests/Editor/Fixtures/core.json`, JSON.stringify({ sourceHashes: {
    'src/core.ts': manifest.modules[0].sha256, 'characters/demo/model.json': sha(fs.readFileSync(path.join(root, 'characters/demo/model.json'))) } }))
  write(`${pkg}/Tests/Editor/CoreTests.cs`, 'Load("core.json");')
  write('scripts/unity/generate-core.mjs', "// --check core.json\nthrow Error('must not execute');")
  const audit = ref => auditUpstream(root, ref)
  return { root, write, git, commit, baseline, manifest, saveManifest, audit }
}

test('unchanged baseline remains clean, pending is explicit, audit preserves files and Git state', t => {
  const s = sandbox(t), files = [`${pkg}/compatibility.json`, `${pkg}/Tests/Editor/Fixtures/core.json`, `${pkg}/Runtime/core.cs`, 'src/core.ts']
  const before = files.map(p => sha(fs.readFileSync(path.join(s.root, p))))
  const state = s.git('status', '--porcelain=v1', '-uall'), head = s.git('rev-parse', 'HEAD')
  const r = s.audit()
  assert.deepEqual(r.issues, []); assert.deepEqual(r.impactedModules, []); assert.equal(r.fixtureCount, 1)
  assert.deepEqual(r.unimplemented, ['src/pending.ts'])
  assert.deepEqual(before, files.map(p => sha(fs.readFileSync(path.join(s.root, p)))))
  assert.equal(s.git('status', '--porcelain=v1', '-uall'), state); assert.equal(s.git('rev-parse', 'HEAD'), head)
})
test('changed dependency reaches type import, reexport and dynamic consumer; references fixture checks', t => {
  const s = sandbox(t); s.write('src/core.ts', 'export type Rate = number; export const rate = 44100;\n')
  const r = s.audit()
  assert.deepEqual(r.sourceChanges.map(c => c.source), ['src/core.ts'])
  assert.deepEqual(r.impactedModules.map(m => m.source), ['src/core.ts', 'src/index.ts', 'src/middle.ts'])
  assert.equal(r.impactedModules.find(m => m.source === 'src/middle.ts').direct, false)
  assert.ok(r.impactedModules[0].reviewFiles.includes(`${pkg}/Runtime/Platform/core.cs`))
  assert.equal(r.fixtureReview.length, 1)
  assert.ok(r.fixtureReview[0].references.includes(`${pkg}/Tests/Editor/CoreTests.cs`))
  assert.deepEqual(r.fixtureReview[0].checkCommands, [['node', '--import', './test/ts.mjs', 'scripts/unity/generate-core.mjs', '--check']])
})
test('added module and removed module remain separate with previous consumers retained', t => {
  const s = sandbox(t); fs.renameSync(path.join(s.root, 'src/core.ts'), path.join(s.root, 'src/renamed.ts'))
  const r = s.audit()
  assert.deepEqual(r.sourceChanges.map(c => c.kind), ['removed-source', 'unmapped-new-source'])
  assert.ok(r.impactedModules.some(m => m.source === 'src/index.ts'))
  assert.equal(r.impactedModules.find(m => m.source === 'src/renamed.ts').status, 'unmapped')
  assert.ok(r.graphNotes.some(n => n.kind === 'unresolved-relative-import'))
})
test('missing port/adapter still detected when corresponding source was removed', t => {
  const s = sandbox(t)
  for (const p of ['src/core.ts', `${pkg}/Runtime/core.cs`, `${pkg}/Runtime/Platform/core.cs`]) fs.unlinkSync(path.join(s.root, p))
  const kinds = s.audit().issues.map(i => i.kind)
  for (const kind of ['removed-source', 'missing-port', 'missing-adapter']) assert.ok(kinds.includes(kind))
})
test('edited manifest hash cannot silently bless modified source; stale fixture rejected', t => {
  const s = sandbox(t), text = 'export const rate = 44100;\n'
  s.write('src/core.ts', text); s.manifest.modules[0].sha256 = sha(text); s.saveManifest()
  s.write(`${pkg}/Tests/Editor/Fixtures/core.json`, JSON.stringify({ sourceHashes: { 'src/core.ts': sha(text) } }))
  const kinds = s.audit().issues.map(i => i.kind)
  assert.ok(kinds.includes('manifest-baseline-mismatch')); assert.ok(kinds.includes('fixture-baseline-mismatch'))
})
test('contract and fixture asset changes are reviewable without any TS change', t => {
  const s = sandbox(t); s.write('API.md', '# API v2\n'); s.write('characters/demo/model.json', '{"name":"updated"}')
  s.write('test/new.test.ts', '// new upstream test')
  const r = s.audit()
  assert.deepEqual(r.sourceChanges, [])
  assert.deepEqual(r.otherChanges.map(c => c.path), ['API.md', 'characters/demo/model.json', 'test/new.test.ts'])
  assert.deepEqual(r.fixtureReview[0].changedInputs.map(c => c.source), ['characters/demo/model.json'])
})
test('removing fixture provenance cannot turn the check green', t => {
  const s = sandbox(t); s.write(`${pkg}/Tests/Editor/Fixtures/core.json`, JSON.stringify({ cases: [] }))
  assert.ok(s.audit().issues.some(i => i.kind === 'fixture-missing-provenance'))
})
test('new upstream build files are detected but Unity port additions are excluded', t => {
  const s = sandbox(t); s.write('scripts/new-build.mjs', '// upstream build change')
  s.write('src/new-shader.glsl', '// upstream shader'); s.write('docs/unity/port.md', 'port documentation')
  s.write('tools/NewTool/tool.txt', 'port tooling')
  assert.deepEqual(s.audit().otherChanges.map(c => c.path), ['scripts/new-build.mjs', 'src/new-shader.glsl'])
})
test('explicit commit is immutable and ignores unrelated dirty candidate working-tree source', t => {
  const s = sandbox(t); s.write('src/core.ts', 'export type Rate = number; export const rate = 48000;\n')
  const ref = s.commit(); s.write('src/index.ts', 'throw new Error("not candidate");')
  const r = s.audit(ref)
  assert.equal(r.candidateCommit, ref); assert.deepEqual(r.sourceChanges.map(c => c.source), ['src/core.ts'])
})
test('cycles terminate; comments/string literals are not import edges; candidate is never run', t => {
  const s = sandbox(t)
  s.write('src/core.ts', "export * from './middle'; export const rate = 1; throw new Error('never run');\n")
  s.write('src/pending.ts', "// import { rate } from './core'\nexport const text = \"import('./core')\";\n")
  const r = s.audit()
  assert.deepEqual(r.impactedModules.find(m => m.source === 'src/pending.ts').causes, ['src/pending.ts'])
  assert.ok(r.impactedModules.find(m => m.source === 'src/index.ts').causes.includes('src/core.ts'))
})
test('syntax errors and nonliteral imports remain visible instead of producing a clean report', t => {
  const s = sandbox(t); s.write('src/core.ts', 'export const x = ;\n'); s.write('src/index.ts', 'const path = "./core"; import(path);')
  const r = s.audit()
  assert.ok(r.issues.some(i => i.kind === 'source-parse-error'))
  assert.ok(r.graphNotes.some(i => i.kind === 'nonliteral-import-needs-review'))
})
test('invalid mapping and unknown Git revision are errors, not an unchanged candidate', t => {
  const s = sandbox(t); assert.throws(() => s.audit('does-not-exist'))
  s.manifest.modules.push(s.manifest.modules[0]); s.saveManifest(); assert.throws(() => s.audit(), /Invalid module mapping/)
})
