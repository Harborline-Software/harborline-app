import { useEffect, useState } from 'react'
import { DataGrid, DetailPanel, ErrorCard, LoadingState, useCanShowMasterDetail } from '@harborline-software/ui-react'
import type { DataGridCellContext, DataGridColumnDef } from '@harborline-software/ui-react'
import type { ViewDefinitionDetail as ViewDefinitionDetailModel, ViewDefinitionSummary, ViewVersionList } from './client/types'
import { ViewDefinitionDetail } from './ViewDefinitionDetail'
import { useViewsAdminClient } from './ViewsAdminClientContext'

export interface ViewsAdminPageProps {
  readonly panelRailCapable?: boolean
}

export function ViewsAdminPage({ panelRailCapable }: ViewsAdminPageProps) {
  // Ticket 154: the capability hook is the one master-detail decider; the prop stays as a test override.
  const canShowMasterDetail = useCanShowMasterDetail()
  const client = useViewsAdminClient()
  const [definitions, setDefinitions] = useState<readonly ViewDefinitionSummary[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<ViewDefinitionSummary | null>(null)
  const [detail, setDetail] = useState<ViewDefinitionDetailModel | null>(null)
  const [versionList, setVersionList] = useState<ViewVersionList | null>(null)
  const [errorLine, setErrorLine] = useState<string | null>(null)

  useEffect(() => {
    void client.listDefinitions().then(setDefinitions).catch((error: unknown) => {
      setLoadError(error instanceof Error ? error.message : String(error))
    })
  }, [client])

  function select(definition: ViewDefinitionSummary) {
    setSelected(definition)
    setDetail(null)
    setVersionList(null)
    setErrorLine(null)
    void client.getDefinition(definition.key).then(setDetail).catch((error: unknown) => {
      setErrorLine(error instanceof Error ? error.message : String(error))
    })
    void client.listVersions(definition.key).then(setVersionList).catch((error: unknown) => {
      setErrorLine(error instanceof Error ? error.message : String(error))
    })
  }

  const columns: readonly DataGridColumnDef<ViewDefinitionSummary>[] = [
    {
      id: 'key',
      header: 'Key',
      field: 'key',
      renderCell: ({ row }: DataGridCellContext<ViewDefinitionSummary>) => (
        <button type="button" className="happ-link" onClick={() => select(row)}>{row.key}</button>
      ),
      removalPriority: 50,
    },
    { id: 'title', header: 'Title', field: 'title', removalPriority: 40 },
    { id: 'version', header: 'Version', field: 'version', removalPriority: 30 },
    { id: 'viewKind', header: 'Kind', field: 'viewKind', removalPriority: 20 },
    { id: 'cascadeLayer', header: 'Cascade layer', field: 'cascadeLayer', removalPriority: 10 },
  ]

  if (loadError) return <><h1>Views</h1><ErrorCard title="Unable to load view definitions" message={loadError} /></>
  if (definitions === null) return <><h1>Views</h1><LoadingState label="Loading view definitions" /></>

  return (
    <>
      <h1>Views</h1>
      <DataGrid<ViewDefinitionSummary>
        accessibleName="View definitions"
        rows={definitions}
        getRowId={row => `${row.key}@${row.version}`}
        zebra
        empty="No view definitions for this tenant."
        columns={columns}
      />
      {selected !== null && (
        <DetailPanel
          label={`View definition ${selected.key}`}
          open
          onOpenChange={open => {
            if (!open) {
              setSelected(null)
              setDetail(null)
              setVersionList(null)
              setErrorLine(null)
            }
          }}
          width={420}
          railCapable={panelRailCapable ?? canShowMasterDetail}
        >
          <ViewDefinitionDetail
            definition={selected}
            detail={detail}
            versionList={versionList}
            errorLine={errorLine}
          />
        </DetailPanel>
      )}
    </>
  )
}
