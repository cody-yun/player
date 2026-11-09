// Candidate code is parsed as data, never executed. The baseline and fixtures are read-only.
import fs from 'node:fs'
import path from 'node:path'
import crypto from 'node:crypto'
import zlib from 'node:zlib'
import { execFileSync } from 'node:child_process'
import { createRequire } from 'node:module'
const require = createRequire(new URL('../../tools/UpstreamAudit/package.json', import.meta.url))
const ts = require('typescript')
const packagePath = 'unity/Packages/ai.livliv.player'
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex')
const sorted = values => [...new Set(values)].sort()
const walk = dir => !fs.existsSync(dir) ? [] : fs.readdirSync(dir, { withFileTypes: true })
  .flatMap(e => e.isDirectory() ? walk(path.join(dir, e.name)) : e.isFile() ? [path.join(dir, e.name)] : [])
// These four directories belong to the Unity port. Everything else is upstream review scope.
const watched = ['.', ':(exclude)unity', ':(exclude)tools', ':(exclude)docs/unity', ':(exclude)scripts/unity']
const mappedSource = name => name.startsWith('src/') && name.endsWith('.ts')

export function auditUpstream(root, ref) {
  root = path.resolve(root)
  const git = (...args) => execFileSync('git', args, { cwd: root, maxBuffer: 32 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] })
  const resolve = value => git('rev-parse', '--verify', '--end-of-options', `${value}^{commit}`).toString().trim()
  const manifest = JSON.parse(fs.readFileSync(path.join(root, packagePath, 'compatibility.json'), 'utf8'))
  if (!/^[a-f0-9]{40}$/.test(manifest.commit) || !Array.isArray(manifest.modules)) throw Error('Invalid compatibility manifest')
  const bySource = new Map()
  const repoPath = target => {
    const full = path.resolve(root, packagePath, target)
    const relative = path.relative(root, full).split(path.sep).join('/')
    if (!relative.startsWith('unity/Packages/')) throw Error(`Port path outside packages: ${target}`)
    return relative
  }
  for (const m of manifest.modules) {
    if (!/^src\/[^\0\r\n]+\.ts$/.test(m.source) || m.source.split('/').includes('..') || bySource.has(m.source)
      || !/^[a-f0-9]{64}$/.test(m.sha256) || !['pending', 'partial', 'ported'].includes(m.status)
      || typeof m.target !== 'string' || (m.adapters && !Array.isArray(m.adapters))) throw Error(`Invalid module mapping: ${m.source}`)
    for (const p of [m.target, ...m.adapters || []]) repoPath(p)
    bySource.set(m.source, m)
  }
  const baseline = resolve(manifest.commit), candidate = ref ? resolve(ref) : null
  const snapshot = commit => {
    const sources = commit
      ? git('ls-tree', '-r', '-z', '--name-only', commit, '--', 'src').toString().split('\0').filter(p => p.endsWith('.ts'))
      : walk(path.join(root, 'src')).filter(p => p.endsWith('.ts')).map(p => path.relative(root, p).split(path.sep).join('/'))
    const cache = new Map()
    const read = name => {
      if (!cache.has(name)) {
        if (commit) {
          try { cache.set(name, git('show', `${commit}:${name}`)) }
          catch (e) {
            const message = e.stderr?.toString() || ''
            if (/does not exist in|exists on disk, but not in/.test(message)) cache.set(name, null)
            else throw e
          }
        } else cache.set(name, fs.existsSync(path.join(root, name)) ? fs.readFileSync(path.join(root, name)) : null)
      }
      return cache.get(name)
    }
    return { sources: sorted(sources), read }
  }
  const before = snapshot(baseline), after = snapshot(candidate), issues = [], sourceChanges = []
  const digest = bytes => bytes === null ? null : hash(bytes)
  for (const m of manifest.modules) {
    const actual = digest(before.read(m.source))
    if (actual !== m.sha256) issues.push({ kind: 'manifest-baseline-mismatch', source: m.source, expected: m.sha256, actual })
    if (m.status !== 'pending' && !fs.existsSync(path.join(root, repoPath(m.target))))
      issues.push({ kind: 'missing-port', source: m.source, target: m.target })
    for (const p of m.adapters || []) if (!fs.existsSync(path.join(root, repoPath(p))))
      issues.push({ kind: 'missing-adapter', source: m.source, target: p })
  }
  for (const source of before.sources) if (!bySource.has(source)) issues.push({ kind: 'unmapped-baseline-source', source })
  for (const source of sorted([...before.sources, ...after.sources])) {
    const oldHash = before.sources.includes(source) ? digest(before.read(source)) : null
    const newHash = after.sources.includes(source) ? digest(after.read(source)) : null
    if (oldHash === newHash) continue
    const kind = !oldHash ? 'unmapped-new-source' : !newHash ? 'removed-source' : 'changed-source'
    const m = bySource.get(source)
    const row = { source, kind, beforeSha256: oldHash, afterSha256: newHash, target: m?.target ?? null, status: m?.status ?? 'unmapped' }
    sourceChanges.push(row); issues.push(row)
  }
  // Union of both graphs retains consumers of removed/renamed modules.
  // Type-only imports, reexports and literal dynamic imports count conservatively.
  const reverse = new Map(), graphNotes = []
  for (const [label, tree] of [['baseline', before], ['candidate', after]]) for (const source of tree.sources) {
    const ast = ts.createSourceFile(source, tree.read(source).toString(), ts.ScriptTarget.Latest, true)
    for (const d of ast.parseDiagnostics) issues.push({ kind: 'source-parse-error', tree: label, source,
      message: ts.flattenDiagnosticMessageText(d.messageText, '\n') })
    const imports = []
    const visit = node => {
      if ((ts.isImportDeclaration(node) || ts.isExportDeclaration(node)) && node.moduleSpecifier) imports.push(node.moduleSpecifier)
      else if (ts.isImportEqualsDeclaration(node) && ts.isExternalModuleReference(node.moduleReference)) imports.push(node.moduleReference.expression)
      else if (ts.isImportTypeNode(node) && ts.isLiteralTypeNode(node.argument)) imports.push(node.argument.literal)
      else if (ts.isCallExpression(node) && node.expression.kind === ts.SyntaxKind.ImportKeyword) imports.push(node.arguments[0])
      ts.forEachChild(node, visit)
    }
    visit(ast)
    for (const spec of imports) {
      if (!spec || !ts.isStringLiteralLike(spec)) { graphNotes.push({ tree: label, source, kind: 'nonliteral-import-needs-review' }); continue }
      if (!spec.text.startsWith('.')) continue
      const base = path.posix.normalize(path.posix.join(path.posix.dirname(source), spec.text))
      const choices = [base, base.replace(/\.[cm]?js$/, '.ts'), `${base}.ts`, `${base}/index.ts`]
      const dependency = choices.find(p => tree.sources.includes(p))
      if (!dependency) { graphNotes.push({ tree: label, source, kind: 'unresolved-relative-import', specifier: spec.text }); continue }
      if (!reverse.has(dependency)) reverse.set(dependency, new Set())
      reverse.get(dependency).add(source)
    }
  }
  const causes = new Map()
  for (const change of sourceChanges) {
    const queue = [change.source], visited = new Set()
    while (queue.length) {
      const source = queue.shift()
      if (visited.has(source)) continue
      visited.add(source)
      if (!causes.has(source)) causes.set(source, new Set())
      causes.get(source).add(change.source)
      queue.push(...reverse.get(source) || [])
    }
  }
  const impactedModules = [...causes.keys()].sort().map(source => {
    const m = bySource.get(source)
    return { source, direct: sourceChanges.some(c => c.source === source), causes: sorted(causes.get(source)),
      status: m?.status ?? 'unmapped', reviewFiles: m ? [m.target, ...m.adapters || []].map(repoPath) : [] }
  })
  const tokens = git('diff', '--name-status', '-z', '--no-renames', baseline, ...candidate ? [candidate] : [], '--', ...watched).toString().split('\0')
  const otherChanges = []
  for (let i = 0; i + 1 < tokens.length; i += 2) if (!mappedSource(tokens[i + 1])) otherChanges.push({ status: tokens[i], path: tokens[i + 1] })
  if (!candidate) for (const name of git('ls-files', '--others', '--exclude-standard', '-z', '--', ...watched).toString().split('\0').filter(Boolean))
    if (!mappedSource(name)) otherChanges.push({ status: 'A', path: name })
  otherChanges.sort((a, b) => a.path.localeCompare(b.path))
  for (const change of otherChanges) issues.push({ kind: 'upstream-contract-or-asset-change', ...change })
  const fixtures = walk(path.join(root, packagePath, 'Tests/Editor/Fixtures')).filter(p => /\.json(?:\.gz)?$/.test(p))
  const references = [...walk(path.join(root, 'unity/Packages')).filter(p => /\/Tests\/.*\.cs$/.test(p)),
    ...walk(path.join(root, 'scripts/unity')).filter(p => /\/(?:test-|generate-|capture-).*\.mjs$/.test(p))]
    .map(p => [path.relative(root, p).split(path.sep).join('/'), fs.readFileSync(p, 'utf8')])
  const fixtureReview = []
  for (const f of fixtures) {
    const bytes = fs.readFileSync(f), data = JSON.parse(f.endsWith('.gz') ? zlib.gunzipSync(bytes) : bytes)
    const file = path.relative(root, f).split(path.sep).join('/'), changedInputs = []
    if (!data.sourceHashes || typeof data.sourceHashes !== 'object' || Array.isArray(data.sourceHashes) || !Object.keys(data.sourceHashes).length) {
      issues.push({ kind: 'fixture-missing-provenance', fixture: file }); continue
    }
    for (const [source, expected] of Object.entries(data.sourceHashes)) {
      if (!/^(src|characters)\//.test(source) || source.split('/').includes('..') || !/^[a-f0-9]{64}$/.test(expected)) throw Error(`Invalid fixture dependency: ${source}`)
      const oldHash = digest(before.read(source)), newHash = digest(after.read(source))
      if (oldHash !== expected) issues.push({ kind: 'fixture-baseline-mismatch', fixture: file, source, expected, actual: oldHash })
      if (oldHash !== newHash) changedInputs.push({ source, beforeSha256: oldHash, afterSha256: newHash })
    }
    const impactedSources = Object.keys(data.sourceHashes).filter(p => causes.has(p))
    if (!changedInputs.length && !impactedSources.length) continue
    const matching = references.filter(([, body]) => body.includes(path.basename(f)))
    fixtureReview.push({ file, changedInputs, impactedSources,
      references: matching.map(([name]) => name),
      checkCommands: matching.filter(([name, body]) => name.includes('/generate-') && body.includes('--check'))
        .map(([name]) => ['node', '--import', './test/ts.mjs', name, '--check']) })
  }
  return { schemaVersion: 1, baseline, comparedTo: ref || 'working-tree', candidateCommit: candidate,
    parserVersion: ts.version, modules: manifest.modules.length, fixtureCount: fixtures.length,
    issues, sourceChanges, otherChanges, impactedModules, fixtureReview, graphNotes,
    unimplemented: manifest.modules.filter(m => m.status === 'pending').map(m => m.source),
    interpretation: 'Read-only review scope, not proof of behavioral equivalence. Pending/partial status is not promoted. Candidate code and fixture generators were not executed.' }
}
