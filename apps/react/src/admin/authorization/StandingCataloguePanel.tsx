import type { StandingDefinition } from './client'

export function StandingCataloguePanel({ standings }: { readonly standings: readonly StandingDefinition[] }) {
  return (
    <section aria-labelledby="standing-catalogue-heading">
      <h2 id="standing-catalogue-heading">Standing Catalogue</h2>
      {standings.length === 0 ? <p>No standing rules are declared.</p> : standings.map(rule => (
        <article key={`${rule.ruleId}\u0000${rule.ruleVersion}`} aria-labelledby={`standing-${rule.ruleId}-${rule.ruleVersion}`} style={{ minWidth: 0 }}>
          <h3 id={`standing-${rule.ruleId}-${rule.ruleVersion}`} style={{ overflowWrap: 'anywhere' }}>{rule.ruleId} · {rule.ruleVersion}</h3>
          <dl>
            <dt>Standing</dt><dd style={{ overflowWrap: 'anywhere' }}>{rule.standing}</dd>
            <dt>Declared record type</dt><dd style={{ overflowWrap: 'anywhere' }}>{rule.declaredRecordType}</dd>
          </dl>
          <h4>Input fields</h4>
          {rule.fields.map(field => (
            <section key={field.field} aria-label={`Standing field ${field.field}`}>
              <h5 style={{ overflowWrap: 'anywhere' }}>{field.field}</h5>
              {field.carryingRecordTypes.length === 0 ? (
                <p role="alert">No carrying record types are declared for this field.</p>
              ) : (
                <ul>{field.carryingRecordTypes.map(recordType => <li key={recordType} style={{ overflowWrap: 'anywhere' }}>{recordType}</li>)}</ul>
              )}
            </section>
          ))}
        </article>
      ))}
    </section>
  )
}
