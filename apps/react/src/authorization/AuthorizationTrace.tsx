import { useState } from 'react'
import { AuthorizationAdminError } from '../admin/authorization/client/types'

// Mirrors GET /api/local-node/authorization/traces/{auditId}.
// This surface renders recorded evidence without evaluating permissions.
export interface AuthorizationTraceRead {
  availability: number
  version: number | null
  steps: readonly { ordinal: number; stage: string; facts: readonly string[] }[]
}
export interface RecordedDecision {
  auditId: string
  read: (auditId: string) => Promise<AuthorizationTraceRead>
}
const stages = ['act', 'effective-roles', 'standings', 'verdict']
const labels = ['Act kind', 'Roles in force for the subject with scope and dates', 'Standings', 'Verdict']

export function AuthorizationTrace({ decision }: { decision?: RecordedDecision }) {
  const [result, setResult] = useState<AuthorizationTraceRead | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  async function load() {
    if (!decision || loading) return
    setLoading(true)
    setResult(null)
    setError(null)
    try { setResult(await decision.read(decision.auditId)) }
    catch (failure) {
      if (failure instanceof AuthorizationAdminError && failure.status === 403) setResult({ availability: 2, version: null, steps: [] })
      else setError('Unable to read the authorization trace. Try again.')
    }
    finally { setLoading(false) }
  }
  const valid = result?.version === 1 && result.steps.length === 4
    && result.steps.every((step, index) => step.ordinal === index + 1 && step.stage === stages[index])
  const deciding = result?.steps[1]?.facts.find(fact => fact.startsWith('deciding:'))?.slice(9)
  // No linked decision, no disclosure: the affordance must never promise an answer it cannot give (163 review 1).
  if (!decision) return null
  return <details onToggle={event => { if (event.currentTarget.open && result === null && error === null) void load() }}>
    <summary>Why can I do this?</summary>
    {<>
      {loading && <p role="status">Loading authorization trace…</p>}
      {error && <p role="alert">{error}</p>}
      {result?.availability === 2 ? <p role="alert">You do not have permission to read this authorization trace.</p>
        : result?.availability === 1 ? <p>No authorization trace was recorded for this decision.</p>
          : result && (!valid || result.availability !== 0) ? <p role="alert">The recorded authorization trace is incomplete or unsupported.</p>
            : result && <ol aria-label="Authorization trace">
              {result.steps.map((step, index) => <li key={step.ordinal}>
                <h4>{labels[index]}</h4>
                {step.facts.map((fact, factIndex) => <p key={factIndex} style={{ overflowWrap: 'anywhere' }}>{fact}</p>)}
                {index === 3 && <p>Deciding grant: {deciding?.startsWith('grant:') ? deciding.slice(6) : 'None recorded'}</p>}
              </li>)}
            </ol>}
      {!loading && (error || result?.availability === 2) && <button type="button" onClick={() => void load()}>Retry trace read</button>}
    </>}
  </details>
}
