import { useEffect, useState } from 'react'
import { ConfirmDialog, DataGrid, DetailPanel, ErrorCard, LoadingState, useCanShowMasterDetail } from '@harborline-software/ui-react'
import type { DataGridCellContext, DataGridColumnDef } from '@harborline-software/ui-react'
import { FormsAdminError } from './client/types'
import type { FormDefinitionSummary, FormVersionSummary } from './client/types'
import { FormDefinitionDetail } from './FormDefinitionDetail'
import { useFormsAdminClient } from './FormsAdminClientContext'
import { formatIt } from './it'

export interface FormsAdminPageProps {
  readonly panelRailCapable?: boolean
}

export function FormsAdminPage({ panelRailCapable }: FormsAdminPageProps) {
  // Ticket 154: the capability hook is the one master-detail decider; the prop stays as a test override.
  const canShowMasterDetail = useCanShowMasterDetail()
  const client = useFormsAdminClient()
  const [definitions, setDefinitions] = useState<readonly FormDefinitionSummary[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<FormDefinitionSummary | null>(null)
  const [versions, setVersions] = useState<readonly FormVersionSummary[] | null>(null)
  const [pendingRestore, setPendingRestore] = useState<string | null>(null)
  const [statusLine, setStatusLine] = useState<string | null>(null)
  const [errorLine, setErrorLine] = useState<string | null>(null)

  useEffect(() => {
    void client.listDefinitions().then(setDefinitions).catch((error: unknown) => {
      setLoadError(error instanceof Error ? error.message : String(error))
    })
  }, [client])

  function select(definition: FormDefinitionSummary) {
    setSelected(definition)
    setVersions(null)
    setStatusLine(null)
    setErrorLine(null)
    void client.listVersions(definition.formId).then(setVersions).catch((error: unknown) => {
      setErrorLine(error instanceof Error ? error.message : String(error))
    })
  }

  async function handleRestore() {
    const v = pendingRestore
    if (!selected || v === null) return
    setPendingRestore(null)
    setErrorLine(null)
    try {
      const result = await client.restoreVersion(selected.formId, v)
      setStatusLine(`Draft ${result.version} created from ${v}.`)
      setVersions(await client.listVersions(selected.formId))
    } catch (error) {
      if (error instanceof FormsAdminError) setErrorLine(error.message)
      else throw error
    }
  }

  const columns: readonly DataGridColumnDef<FormDefinitionSummary>[] = [
    {
      id: 'key',
      header: 'Key',
      field: 'formId',
      renderCell: ({ row }: DataGridCellContext<FormDefinitionSummary>) => (
        <button type="button" className="happ-link" onClick={() => select(row)}>{row.formId}</button>
      ),
      removalPriority: 40,
    },
    { id: 'title', header: 'Title', field: row => formatIt(row.title), removalPriority: 30 },
    { id: 'version', header: 'Version', field: 'version', removalPriority: 20 },
    { id: 'cascadeLayer', header: 'Cascade layer', field: row => row.cascadeLayer ?? '—', removalPriority: 10 },
  ]

  if (loadError) return <><h1>Forms</h1><ErrorCard title="Unable to load form definitions" message={loadError} /></>
  if (definitions === null) return <><h1>Forms</h1><LoadingState label="Loading form definitions" /></>

  return (
    <>
      <h1>Forms</h1>
      <DataGrid<FormDefinitionSummary>
        accessibleName="Form definitions"
        rows={definitions}
        getRowId={definition => `${definition.formId}@${definition.version}`}
        zebra
        empty="No form definitions for this tenant."
        columns={columns}
      />
      {selected !== null && (
        <>
          <DetailPanel
            label={`Form definition ${selected.formId}`}
            open
            onOpenChange={open => {
              if (!open) {
                setSelected(null)
                setVersions(null)
                setStatusLine(null)
                setErrorLine(null)
                setPendingRestore(null)
              }
            }}
            width={420}
            railCapable={panelRailCapable ?? canShowMasterDetail}
          >
            <FormDefinitionDetail
              definition={selected}
              versions={versions}
              statusLine={statusLine}
              errorLine={errorLine}
              onRestore={setPendingRestore}
            />
          </DetailPanel>
          <ConfirmDialog
            open={pendingRestore !== null}
            onOpenChange={open => { if (!open) setPendingRestore(null) }}
            title={`Restore ${pendingRestore}?`}
            description={`Creates a new draft of '${selected.formId}' derived from ${pendingRestore}. Nothing is published and history is never modified. The draft syncs to peers.`}
            confirmLabel="Restore as draft"
            onConfirm={() => { void handleRestore() }}
          />
        </>
      )}
    </>
  )
}
