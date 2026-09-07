import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeaderCell,
  TableRow,
} from '@harborline-software/ui-react'
import type { FormDefinitionSummary, FormVersionSummary } from './client/types'
import { formatIt, formatUtc } from './it'

export interface FormDefinitionDetailProps {
  readonly definition: FormDefinitionSummary
  readonly versions: readonly FormVersionSummary[] | null
  readonly statusLine: string | null
  readonly errorLine?: string | null
  readonly onRestore: (version: string) => void
}

export function FormDefinitionDetail({ definition, versions, statusLine, errorLine, onRestore }: FormDefinitionDetailProps) {
  const head = versions?.find(row => row.version === definition.version)

  return (
    <div>
      <dl>
        <dt>Form id</dt><dd>{definition.formId}</dd>
        <dt>Title</dt><dd>{formatIt(definition.title)}</dd>
        <dt>Published version</dt><dd>{definition.version}</dd>
        <dt>Cascade layer</dt><dd>{definition.cascadeLayer ?? '—'}</dd>
        <dt>Owner</dt><dd>{head?.owner ?? '—'}</dd>
        <dt>Created</dt><dd>{head ? formatUtc(head.createdAt) : '—'}</dd>
        <dt>Updated</dt><dd>{formatUtc(definition.updatedAt)}</dd>
        <dt>Derived from</dt><dd>{head?.derivedFrom ?? '—'}</dd>
        <dt>Syncs to peers</dt><dd>{head ? (head.syncsToPeers ? 'Yes' : 'No') : '—'}</dd>
        <dt>Safe for staging</dt><dd>{head ? (head.safeForStaging ? 'Yes' : 'No') : '—'}</dd>
      </dl>
      {statusLine && <p data-testid="restore-status">{statusLine}</p>}
      {errorLine && <p role="alert">{errorLine}</p>}
      {versions === null ? (
        <p>Loading version history…</p>
      ) : (
        <Table density="sm">
          <TableCaption>Version history</TableCaption>
          <TableHead>
            <TableRow>
              <TableHeaderCell>Version</TableHeaderCell>
              <TableHeaderCell>Status</TableHeaderCell>
              <TableHeaderCell>Owner</TableHeaderCell>
              <TableHeaderCell>Updated</TableHeaderCell>
              <TableHeaderCell>Derived from</TableHeaderCell>
              <TableHeaderCell>Actions</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {versions.map(row => (
              <TableRow key={row.version}>
                <TableCell>{row.version}</TableCell>
                <TableCell>{row.status}</TableCell>
                <TableCell>{row.owner ?? '—'}</TableCell>
                <TableCell>{formatUtc(row.updatedAt)}</TableCell>
                <TableCell>{row.derivedFrom ?? '—'}</TableCell>
                <TableCell><button type="button" onClick={() => onRestore(row.version)}>Restore…</button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
