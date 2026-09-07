import { useEffect, useState } from 'react'
import { DataGrid, DetailPanel, ErrorCard, LoadingState, useCanShowMasterDetail } from '@harborline-software/ui-react'
import type { DataGridCellContext, DataGridColumnDef } from '@harborline-software/ui-react'
import type { ReportDefinitionDetail as ReportDefinitionDetailModel, ReportDefinitionSummary, ReportVersionList } from './client/types'
import { ReportDefinitionDetail } from './ReportDefinitionDetail'
import { useReportsAdminClient } from './ReportsAdminClientContext'

export interface ReportsAdminPageProps {
  readonly panelRailCapable?: boolean
}

export function ReportsAdminPage({ panelRailCapable }: ReportsAdminPageProps) {
  // Ticket 154: the capability hook is the one master-detail decider; the prop stays as a test override.
  const canShowMasterDetail = useCanShowMasterDetail()
  const client = useReportsAdminClient()
  const [definitions, setDefinitions] = useState<readonly ReportDefinitionSummary[] | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selected, setSelected] = useState<ReportDefinitionSummary | null>(null)
  const [detail, setDetail] = useState<ReportDefinitionDetailModel | null>(null)
  const [versionList, setVersionList] = useState<ReportVersionList | null>(null)
  const [errorLine, setErrorLine] = useState<string | null>(null)

  useEffect(() => {
    void client.listDefinitions().then(setDefinitions).catch((error: unknown) => {
      setLoadError(error instanceof Error ? error.message : String(error))
    })
  }, [client])

  function select(definition: ReportDefinitionSummary) {
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

  const columns: readonly DataGridColumnDef<ReportDefinitionSummary>[] = [
    {
      id: 'key',
      header: 'Key',
      field: 'key',
      renderCell: ({ row }: DataGridCellContext<ReportDefinitionSummary>) => (
        <button type="button" className="happ-link" onClick={() => select(row)}>{row.key}</button>
      ),
      removalPriority: 50,
    },
    { id: 'title', header: 'Title', field: 'title', removalPriority: 40 },
    { id: 'version', header: 'Version', field: 'version', removalPriority: 30 },
    { id: 'reportKind', header: 'Kind', field: 'reportKind', removalPriority: 20 },
    { id: 'cascadeLayer', header: 'Cascade layer', field: 'cascadeLayer', removalPriority: 10 },
  ]

  if (loadError) return <><h1>Reports</h1><ErrorCard title="Unable to load report definitions" message={loadError} /></>
  if (definitions === null) return <><h1>Reports</h1><LoadingState label="Loading report definitions" /></>

  return (
    <>
      <h1>Reports</h1>
      <DataGrid<ReportDefinitionSummary>
        accessibleName="Report definitions"
        rows={definitions}
        getRowId={row => `${row.key}@${row.version}`}
        zebra
        empty="No report definitions for this tenant."
        columns={columns}
      />
      {selected !== null && (
        <DetailPanel
          label={`Report definition ${selected.key}`}
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
          <ReportDefinitionDetail
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
