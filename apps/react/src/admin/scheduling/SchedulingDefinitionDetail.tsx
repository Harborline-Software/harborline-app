import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeaderCell,
  TableRow,
} from '@harborline-software/ui-react'
import type { SchedulingDefinitionSummary, SchedulingDefinitionView } from './client/types'
import { formatUtc } from './format'

export interface SchedulingDefinitionDetailProps {
  readonly definition: SchedulingDefinitionSummary
  readonly view: SchedulingDefinitionView | null
  readonly versions: readonly SchedulingDefinitionSummary[] | null
  readonly statusLine: string | null
  readonly errorLine?: string | null
  readonly onRestore: (revision: number) => void
}

export function SchedulingDefinitionDetail({ definition, view, versions, statusLine, errorLine, onRestore }: SchedulingDefinitionDetailProps) {
  return (
    <div>
      <dl>
        <dt>Definition id</dt><dd>{definition.id}</dd>
        <dt>Title</dt><dd>{definition.title}</dd>
        <dt>Head revision</dt><dd>{definition.revision}</dd>
        <dt>Updated</dt><dd>{formatUtc(definition.updatedAt)}</dd>
        <dt>Updated by</dt><dd>{definition.updatedBy}</dd>
      </dl>
      {statusLine && <p data-testid="restore-status">{statusLine}</p>}
      {errorLine && <p role="alert">{errorLine}</p>}
      {view === null ? (
        <p>Loading definition…</p>
      ) : (
        <><h2>Definition</h2><pre>{JSON.stringify(view.definition, null, 2)}</pre></>
      )}
      {versions === null ? (
        <p>Loading revision history…</p>
      ) : (
        <Table density="sm">
          <TableCaption>Revision history</TableCaption>
          <TableHead>
            <TableRow>
              <TableHeaderCell>Revision</TableHeaderCell>
              <TableHeaderCell>Updated</TableHeaderCell>
              <TableHeaderCell>Updated by</TableHeaderCell>
              <TableHeaderCell>Actions</TableHeaderCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {versions.map(row => (
              <TableRow key={row.revision}>
                <TableCell>{row.revision}</TableCell>
                <TableCell>{formatUtc(row.updatedAt)}</TableCell>
                <TableCell>{row.updatedBy}</TableCell>
                <TableCell><button type="button" onClick={() => onRestore(row.revision)}>Restore…</button></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      )}
    </div>
  )
}
