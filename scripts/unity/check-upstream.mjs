// Read-only audit. Never advances the baseline or overwrites translated code/fixtures.
import { fileURLToPath } from 'node:url'
const args = process.argv.slice(2)
try {
  if (args.length && (args.length !== 2 || args[0] !== '--ref' || !args[1] || args[1].startsWith('-')))
    throw Error('Usage: node scripts/unity/check-upstream.mjs [--ref upstream/main]')
  const { auditUpstream } = await import('./upstream-audit.mjs')
  const report = auditUpstream(fileURLToPath(new URL('../../', import.meta.url)), args[1])
  console.log(JSON.stringify(report, null, 2))
  process.exitCode = report.issues.length ? 1 : 0
} catch (error) {
  console.error(JSON.stringify({ error: error.message, setup: 'npm --prefix tools/UpstreamAudit ci --ignore-scripts' }))
  process.exitCode = 2
}
