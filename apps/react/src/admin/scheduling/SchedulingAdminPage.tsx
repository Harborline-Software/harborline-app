import { useEffect, useState } from 'react'
import { ConfirmDialog, DataGrid, DetailPanel, ErrorCard, LoadingState, useCanShowMasterDetail } from '@harborline-software/ui-react'
import type { DataGridCellContext, DataGridColumnDef } from '@harborline-software/ui-react'
import { SchedulingAdminError } from './client/types'
import type { SchedulingDefinitionSummary, SchedulingDefinitionView } from './client/types'
import { formatUtc } from './format'
import { SchedulingDefinitionDetail } from './SchedulingDefinitionDetail'
import { useSchedulingAdminClient } from './SchedulingAdminClientContext'

export interface SchedulingAdminPageProps {
  readonly panelRailCapable?: boolean
}

export function SchedulingAdminPage({ panelRailCapable }: SchedulingAdminPageProps) {
  // Ticket 154: the capability hook is the one master-detail decider; the prop stays as a test override.
  const canShowMasterDetail = useCanShowMasterDetail()
  const client = useSchedulingAdminClient()
  const [definitions, setDefinitions] = useState<readonly SchedulingDefinitionSummary[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<SchedulingDefinitionSummary | null>(null)
  const [view, setView] = useState<SchedulingDefinitionView | null>(null)
  const [versions, setVersions] = useState<readonly SchedulingDefinitionSummary[] | null>(null)
  const [pendingRestore, setPendingRestore] = useState<number | null>(null)
  const [statusLine, setStatusLine] = useState<string | null>(null)
  const [errorLine, setErrorLine] = useState<string | null>(null)

  useEffect(() => {
    void client.listDefinitions().then(setDefinitions).catch((error: unknown) => {
      setLoadError(error instanceof Error ? error.message : String(error))
    })
  }, [client])

  function select(definition: SchedulingDefinitionSummary) {
    setSelected(definition)
    setView(null)
    setVersions(null)
    setStatusLine(null)
    setErrorLine(null)
    void client.getDefinition(definition.id).then(setView).catch((error: unknown) => {
      setErrorLine(error instanceof Error ? error.message : String(error))
    })
    void client.listVersions(definition.id).then(setVersions).catch((error: unknown) => {
      setErrorLine(error instanceof Error ? error.message : String(error))
    })
  }

  async function handleRestore() {
    const revision = pendingRestore
    if (!selected || revision === null) return
    setPendingRestore(null)
    setErrorLine(null)
    try {
      const result = await client.restoreRevision(selected.id, revision)
      setStatusLine(`Revision ${result.revision} restored from ${result.restoredFrom}.`)
      // No Draft/Published split here: the restored revision IS the new head immediately, so the
      // DEFINITIONS LIST refreshes alongside the history — a deliberate divergence from Forms,
      // where the published head never moves on restore (ticket 088 acceptance).
      const [defs, history, refreshedView] = await Promise.all([
        client.listDefinitions(),
        client.listVersions(selected.id),
        client.getDefinition(selected.id),
      ])
      setDefinitions(defs)
      setVersions(history)
      setView(refreshedView)
      const refreshed = defs.find(row => row.id === selected.id)
      if (refreshed) setSelected(refreshed)
    } catch (error) {
      if (error instanceof SchedulingAdminError) setErrorLine(error.message)
      else throw error
    }
  }

  const columns: readonly DataGridColumnDef<SchedulingDefinitionSummary>[] = [
    {
      id: 'id',
      header: 'Id',
      field: 'id',
      renderCell: ({ row }: DataGridCellContext<SchedulingDefinitionSummary>) => (
        <button type="button" className="happ-link" onClick={() => select(row)}>{row.id}</button>
      ),
      removalPriority: 50,
    },
    { id: 'title', header: 'Title', field: 'title', removalPriority: 40 },
    { id: 'revision', header: 'Revision', field: row => String(row.revision), removalPriority: 30 },
    { id: 'updated', header: 'Updated', field: row => formatUtc(row.updatedAt), removalPriority: 20 },
    { id: 'updatedBy', header: 'Updated by', field: 'updatedBy', removalPriority: 10 },
  ]

  if (loadError) return <><h1>Scheduling</h1><ErrorCard title="Unable to load scheduling definitions" message={loadError} /></>
  if (definitions === null) return <><h1>Scheduling</h1><LoadingState label="Loading scheduling definitions" /></>

  return (
    <>
      <h1>Scheduling</h1>
      <DataGrid<SchedulingDefinitionSummary>
        accessibleName="Scheduling definitions"
        rows={definitions}
        getRowId={definition => `${definition.id}@${definition.revision}`}
        zebra
        empty="No scheduling definitions for this tenant."
        columns={columns}
      />
      {selected !== null && (
        <>
          <DetailPanel
            label={`Scheduling definition ${selected.id}`}
            open
            onOpenChange={open => {
              if (!open) {
                setSelected(null)
                setView(null)
                setVersions(null)
                setStatusLine(null)
                setErrorLine(null)
                setPendingRestore(null)
              }
            }}
            width={420}
            railCapable={panelRailCapable ?? canShowMasterDetail}
          >
            <SchedulingDefinitionDetail
              definition={selected}
              view={view}
              versions={versions}
              statusLine={statusLine}
              errorLine={errorLine}
              onRestore={setPendingRestore}
            />
          </DetailPanel>
          <ConfirmDialog
            open={pendingRestore !== null}
            onOpenChange={open => { if (!open) setPendingRestore(null) }}
            title={`Restore revision ${pendingRestore}?`}
            description={`Appends revision ${pendingRestore}'s content of '${selected.id}' as the new head revision, effective immediately. History is never modified.`}
            confirmLabel="Restore as head"
            onConfirm={() => { void handleRestore() }}
          />
        </>
      )}
    </>
  )
}
