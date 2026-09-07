import { useEffect, useState } from 'react'
import { DataGrid, DetailPanel, ErrorCard, LoadingState, useCanShowMasterDetail } from '@harborline-software/ui-react'
import type { DataGridCellContext, DataGridColumnDef } from '@harborline-software/ui-react'
import type { DataExchangeDefinitionDetail as DataExchangeDefinitionDetailModel, DataExchangeDefinitionSummary, DataExchangeVersionList } from './client/types'
import { DataExchangeDefinitionDetail } from './DataExchangeDefinitionDetail'
import { useDataExchangeAdminClient } from './DataExchangeAdminClientContext'

export interface DataExchangeAdminPageProps {
  readonly panelRailCapable?: boolean
}

export function DataExchangeAdminPage({ panelRailCapable }: DataExchangeAdminPageProps) {
  // Ticket 154: the capability hook is the one master-detail decider; the prop stays as a test override.
  const canShowMasterDetail = useCanShowMasterDetail()
  const client = useDataExchangeAdminClient()
  const [definitions, setDefinitions] = useState<readonly DataExchangeDefinitionSummary[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<DataExchangeDefinitionSummary | null>(null)
  const [detail, setDetail] = useState<DataExchangeDefinitionDetailModel | null>(null)
  const [versionList, setVersionList] = useState<DataExchangeVersionList | null>(null)
  const [errorLine, setErrorLine] = useState<string | null>(null)

  useEffect(() => {
    void client.listDefinitions().then(setDefinitions).catch((error: unknown) => {
      setLoadError(error instanceof Error ? error.message : String(error))
    })
  }, [client])

  function select(definition: DataExchangeDefinitionSummary) {
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

  const columns: readonly DataGridColumnDef<DataExchangeDefinitionSummary>[] = [
    {
      id: 'key',
      header: 'Key',
      field: 'key',
      renderCell: ({ row }: DataGridCellContext<DataExchangeDefinitionSummary>) => (
        <button type="button" className="happ-link" onClick={() => select(row)}>{row.key}</button>
      ),
      removalPriority: 50,
    },
    { id: 'title', header: 'Title', field: 'title', removalPriority: 40 },
    { id: 'version', header: 'Version', field: 'version', removalPriority: 30 },
    { id: 'exchangeKind', header: 'Kind', field: 'exchangeKind', removalPriority: 20 },
    { id: 'cascadeLayer', header: 'Cascade layer', field: 'cascadeLayer', removalPriority: 10 },
  ]

  if (loadError) return <><h1>Data Exchange</h1><ErrorCard title="Unable to load data exchange definitions" message={loadError} /></>
  if (definitions === null) return <><h1>Data Exchange</h1><LoadingState label="Loading data exchange definitions" /></>

  return (
    <>
      <h1>Data Exchange</h1>
      <DataGrid<DataExchangeDefinitionSummary>
        accessibleName="Data exchange definitions"
        rows={definitions}
        getRowId={row => `${row.key}@${row.version}`}
        zebra
        empty="No data exchange definitions for this tenant."
        columns={columns}
      />
      {selected !== null && (
        <DetailPanel
          label={`Data exchange definition ${selected.key}`}
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
          <DataExchangeDefinitionDetail
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
