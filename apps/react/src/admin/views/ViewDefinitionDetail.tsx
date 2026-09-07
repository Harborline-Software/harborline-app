import {
  Table,
  TableBody,
  TableCaption,
  TableCell,
  TableHead,
  TableHeaderCell,
  TableRow,
} from '@harborline-software/ui-react'
import type { ViewDefinitionDetail as ViewDefinitionDetailModel, ViewDefinitionSummary, ViewVersionList } from './client/types'

export interface ViewDefinitionDetailProps {
  readonly definition: ViewDefinitionSummary
  readonly detail: ViewDefinitionDetailModel | null
  readonly versionList: ViewVersionList | null
  readonly errorLine?: string | null
}

export function ViewDefinitionDetail({ definition, detail, versionList, errorLine }: ViewDefinitionDetailProps) {
  return (
    <div>
      <dl>
        <dt>Key</dt><dd>{definition.key}</dd>
        <dt>Title</dt><dd>{definition.title}</dd>
        <dt>Published version</dt><dd>{definition.version}</dd>
        <dt>Kind</dt><dd>{definition.viewKind}</dd>
        <dt>Cascade layer</dt><dd>{definition.cascadeLayer}</dd>
        <dt>Schema version</dt><dd>{detail ? detail.schemaVersion : '—'}</dd>
      </dl>
      {errorLine && <p role="alert">{errorLine}</p>}
      {detail === null ? (
        <p>Loading definition detail…</p>
      ) : (
        <>
          <h2>Parameters</h2><pre>{JSON.stringify(detail.parameters, null, 2)}</pre>
          <h2>Provenance</h2><pre>{JSON.stringify(detail.provenance, null, 2)}</pre>
        </>
      )}
      {versionList === null ? (
        <p>Loading version history…</p>
      ) : (
        <>
          <p>{versionList.ordering === 'semver' ? 'Ordered by semver' : 'Ordered ordinally'}</p>
          <Table density="sm">
            <TableCaption>Version history</TableCaption>
            <TableHead>
              <TableRow>
                <TableHeaderCell>Version</TableHeaderCell>
                <TableHeaderCell>Kind</TableHeaderCell>
                <TableHeaderCell>Cascade layer</TableHeaderCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {versionList.versions.map(row => (
                <TableRow key={row.version}>
                  <TableCell>{row.version}</TableCell>
                  <TableCell>{row.viewKind}</TableCell>
                  <TableCell>{row.cascadeLayer}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </>
      )}
    </div>
  )
}
