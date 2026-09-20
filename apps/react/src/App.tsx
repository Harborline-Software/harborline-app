import { AccessHoldersPage } from './admin/authorization/AccessHoldersPage'
import { useCallback, useEffect, useState } from 'react'
import { readPackNavigation } from './navigation/packNavigation'
import { RoleVocabulary, type HeldRoleSet } from '@harborline-software/contracts/authorization'
import { AppShell, type PackNavigationDeclaration, type ShellNavigationState, type ShellNavItem, type ViewRuntimeRow } from '@harborline-software/ui-react'
import { AuthorizationAdminPage } from './admin/authorization/AuthorizationAdminPage'
import { AuthorizationAdminClientProvider } from './admin/authorization/AuthorizationAdminClientContext'
import { createAuthorizationAdminClient, type AuthorizationAdminClient } from './admin/authorization/client'
import { ConfigurationActivationPage } from './admin/configuration/ConfigurationActivationPage'
import { createHttpConfigurationActivationClient } from './admin/configuration/client/httpClient'
import { ConfigurationProposalPage } from './admin/configuration/ConfigurationProposalPage'
import { createHttpConfigurationProposalClient } from './admin/configuration/client/proposalClient'
import { SeededListPage } from './workshop/SeededListPage'
import { ViewAuthoringPage } from './workshop/ViewAuthoringPage'

// Composed from the platform's hlp.ui.app-shell module rather than hand-written chrome, and kept
// deliberately identical to apps/blazor/Shell.razor: same workspace, same item ids, same labels,
// same activation-driven (not href-driven) navigation, same body response to selection, same
// Pilot-panel placeholder behind the header sparkle action. These two files are one application
// in two lanes; when they disagree, one of them is wrong.
let authorizationAdminClient: AuthorizationAdminClient | null = null
let authorizationAdminConfigError: Error | null = null
try {
  authorizationAdminClient = createAuthorizationAdminClient()
} catch (error) {
  authorizationAdminConfigError = error instanceof Error ? error : new Error(String(error))
}

// T-657. The configuration activation routes are served by the same local node origin as the
// authorization admin ones, so this surface reuses it rather than adding a second configured origin.
// The api projects CONFIGURATION_ITEM_ID only to a caller its routes would admit, so an unconfigured
// origin never reaches this client: without a declaration there is no entry to activate.
const configurationActivationClient = createHttpConfigurationActivationClient({
  baseUrl: import.meta.env.DEV ? '' : import.meta.env.VITE_AUTHORIZATION_API_ORIGIN ?? '',
})

// T-668. The proposed-change routes are served by the same local node origin, for the same reason.
const configurationProposalClient = createHttpConfigurationProposalClient({
  baseUrl: import.meta.env.DEV ? '' : import.meta.env.VITE_AUTHORIZATION_API_ORIGIN ?? '',
})

/** The api's navigation item id for the governed configuration activation surface. */
const CONFIGURATION_ITEM_ID = 'configuration.activation'

/** The api's navigation item id for the governed proposed-change surface (T-668). */
const PROPOSAL_ITEM_ID = 'configuration.proposal'

const EMPTY_ROLE_VOCABULARY = RoleVocabulary.fromApi([])
const EMPTY_HELD_ROLES: HeldRoleSet = { roles: [] }

const NAV_ITEMS: readonly ShellNavItem[] = [
  { id: 'assets', label: 'Assets' },
  { id: 'run-report', label: 'Run report' },
  // Ticket 205 will gate this item from live scoped held roles. No existing admin item has
  // a server/session role snapshot yet, so the API route fence remains authoritative.
  { id: 'admin-authorization', label: 'Settings' },
]
const NAVIGATION: PackNavigationDeclaration = {
  seedWorkspaces: [
  {
    id: 'portfolio',
    labelKey: 'Portfolio',
    groups: [
      {
        id: 'portfolio-group',
        labelKey: 'Portfolio',
        itemIds: NAV_ITEMS.map(item => item.id),
      },
    ],
  },
  ],
  panelSet: [{ id: 'pilot', labelKey: 'Pilot', binding: 'panels.pilot.toggle', shortcut: 'mod+shift+p', defaultWidth: 400, minimumHeight: 300, defaultOpen: false }],
}
const workshopLabels: Record<string, string> = {
  'panels.inspector': 'Inspector', 'panels.inspector.footerClaim': 'Follows selection',
  'workshop.workspace': 'Workshop', 'workshop.definitions': 'Definitions', 'workshop.asset-types': 'Record types',
  'workshop.forms': 'Forms', 'workshop.workflows': 'Workflows', 'workshop.standards': 'Standards',
  'workshop.defaults': 'Defaults', 'workshop.terminology': 'Terminology', 'workshop.documents': 'Documents',
  'workshop.taxonomies': 'Taxonomies', 'workshop.reports': 'Reports', 'workshop.data-exchanges': 'Data exchanges',
  'workshop.standing-rules': 'Standing rules', 'workshop.schedules': 'Schedules', 'workshop.views': 'Views',
}
const accessLabels: Record<string, string> = { 'access.workspace': 'Access', 'access.holders': 'Holders', 'access.details': 'Access details', 'access.details.footer': 'Access details' }
const configurationLabels: Record<string, string> = { 'configuration.workspace': 'Configuration', 'configuration.activation': 'Activation', 'configuration.proposal': 'Proposed change' }
const resolveLabel = (key: string) => ({ ...accessLabels, ...configurationLabels, ...workshopLabels })[key] ?? key
// T-585 item 1. This was a hand-written set of thirteen ids, and a hand-written set is how a
// fourteenth member stops reaching the shared shell without anyone noticing: an item the pack
// declared and this list did not fell through to the generic body below and rendered a placeholder,
// silently, with no name and no error. The Workshop membership is the pack's to declare, so it is
// read from the declaration; the seeded set stays as the no-declaration fallback only.
const WORKSHOP_WORKSPACE_ID = 'workshop'
const SEEDED_WORKSHOP_ITEM_IDS = ['asset-types', 'forms', 'workflows', 'standards', 'defaults', 'terminology', 'documents', 'taxonomies', 'reports', 'data-exchanges', 'standing-rules', 'schedules', 'views']
function workshopItemIds(navigation: PackNavigationDeclaration | null): ReadonlySet<string> {
  const declared = (navigation?.seedWorkspaces ?? [])
    .filter(workspace => workspace.id === WORKSHOP_WORKSPACE_ID)
    .flatMap(workspace => workspace.groups ?? [])
    .flatMap(group => group.itemIds)
  return new Set(navigation === null ? SEEDED_WORKSHOP_ITEM_IDS : declared)
}

const NAVIGATION_STATE: ShellNavigationState = {
  items: Object.fromEntries(NAV_ITEMS.map(item => [item.id, item])),
}

const BODY: Record<string, { title: string; description: string }> = {
  'access.holders': { title: 'Holders', description: 'Inspect active access grants.' },
  [CONFIGURATION_ITEM_ID]: {
    title: 'Configuration activation',
    description: 'Inspect the effective configuration generation and the reported activation outcome.',
  },
  [PROPOSAL_ITEM_ID]: {
    title: 'Proposed change',
    description: 'Evolve the effective generation away from operational users, then save and release it.',
  },
  assets: {
    title: 'Assets',
    description: 'Browse and manage the physical assets your organization tracks.',
  },
  'run-report': {
    title: 'Run report',
    description: 'Generate a report across the assets your organization tracks.',
  },
  'admin-authorization': {
    title: 'Settings › System',
    description: 'Administer capability-role bindings and inspect the standing catalogue.',
  },
}

interface ChromeAddress {
  readonly activeItemId: string
  readonly hasItemState: boolean
  readonly selectedRowId: string | null
  readonly openPanelIds: readonly string[]
  readonly hasPanelState: boolean
  readonly surface: 'health' | 'browse' | 'platform.editor.views' | null
  readonly proposalId: string
}

function readChromeAddress(): ChromeAddress {
  const parameters = new URLSearchParams(window.location.search)
  const surface = parameters.get('surface')
  return {
    activeItemId: parameters.get('item') ?? 'assets',
    // T-668. The entry addresses this install's one working proposed change, named by the entry's own
    // id; `?proposal=<id>` addresses any other. The api has no proposal-listing route, and inventing a
    // picker here would be the new surface this ticket is not.
    proposalId: parameters.get('proposal') || PROPOSAL_ITEM_ID,
    hasItemState: parameters.has('item'),
    selectedRowId: parameters.get('selected'),
    openPanelIds: parameters.get('panels')?.split(',').filter(Boolean) ?? [],
    hasPanelState: parameters.has('panels'),
    surface: surface === 'health' || surface === 'browse' || surface === 'platform.editor.views' ? surface : null,
  }
}

function declaredItemIds(navigation: PackNavigationDeclaration): readonly string[] {
  return navigation.seedWorkspaces.flatMap(workspace => (workspace.groups ?? []).flatMap(group => group.itemIds))
}

function displayTitle(row: ViewRuntimeRow): string {
  return String(row.title ?? '').trim() || row.id
}

export function App() {
  const [initialAddress] = useState(readChromeAddress)
  const [activeItemId, setActiveItemId] = useState(initialAddress.activeItemId)
  const [openPanelIds, setOpenPanelIds] = useState<readonly string[]>(initialAddress.openPanelIds)
  const [selectedRowId, setSelectedRowId] = useState<string | null>(initialAddress.selectedRowId)
  const [selectedDefinition, setSelectedDefinition] = useState<ViewRuntimeRow | null>(null)
  const [packNavigation, setPackNavigation] = useState<PackNavigationDeclaration | null>(null)
  const [navigationError, setNavigationError] = useState<string | null>(null)
  const [navigationAttempt, setNavigationAttempt] = useState(0)
  const [roleVocabulary, setRoleVocabulary] = useState(EMPTY_ROLE_VOCABULARY)
  const body = BODY[activeItemId] ?? { title: resolveLabel(`workshop.${activeItemId}`), description: 'Definitions supplied by the active platform pack.' }
  const activeWorkspace = (packNavigation ?? NAVIGATION).seedWorkspaces.find(workspace =>
    workspace.groups?.some(group => group.itemIds.includes(activeItemId)))
  const contentWorkspaceLabel = activeWorkspace ? resolveLabel(activeWorkspace.labelKey) : activeItemId === 'assets' ? 'Portfolio' : null

  useEffect(() => {
    if (authorizationAdminClient === null) return
    const abort = new AbortController()
    setNavigationError(null)
    void readPackNavigation(abort.signal).then(declaration => {
      if (abort.signal.aborted) return
      setPackNavigation(declaration)
      const navigation = declaration ?? NAVIGATION
      const panelIds = new Set(navigation.panelSet?.map(panel => panel.id) ?? [])
      setOpenPanelIds(current => initialAddress.hasPanelState
        ? current.filter(id => panelIds.has(id))
        : navigation.panelSet?.filter(panel => panel.defaultOpen).map(panel => panel.id) ?? [])
      setActiveItemId(current => {
        if (!initialAddress.hasItemState) return current
        const itemIds = declaredItemIds(navigation)
        return itemIds.includes(current) ? current : 'assets'
      })
    }).catch((error: unknown) => {
      if (!abort.signal.aborted) setNavigationError(error instanceof Error ? error.message : 'Unable to load application navigation.')
    })
    return () => abort.abort()
  }, [navigationAttempt])

  useEffect(() => {
    if (!workshopItemIds(packNavigation).has(activeItemId)) {
      setSelectedRowId(null)
      setSelectedDefinition(null)
    }
  }, [activeItemId, packNavigation])

  useEffect(() => {
    const parameters = new URLSearchParams(window.location.search)
    parameters.set('item', activeItemId)
    if (selectedRowId) parameters.set('selected', selectedRowId); else parameters.delete('selected')
    if (openPanelIds.length) parameters.set('panels', openPanelIds.join(',')); else parameters.delete('panels')
    const query = parameters.toString()
    window.history.replaceState(window.history.state, '', `${window.location.pathname}${query ? `?${query}` : ''}${window.location.hash}`)
  }, [activeItemId, openPanelIds, selectedRowId])

  useEffect(() => {
    const abort = new AbortController()
    if (authorizationAdminClient !== null) {
      void authorizationAdminClient.listRoleVocabulary(abort.signal)
        .then(definitions => setRoleVocabulary(RoleVocabulary.fromApi(definitions)))
        .catch(() => setRoleVocabulary(EMPTY_ROLE_VOCABULARY))
    }
    return () => abort.abort()
  }, [])

  const openInspector = useCallback(() => {
    if (!(packNavigation ?? NAVIGATION).panelSet?.some(panel => panel.id === 'inspector')) return
    setOpenPanelIds(current => current.includes('inspector') ? current : [...current, 'inspector'])
  }, [packNavigation])
  const inspectDefinition = useCallback((row: ViewRuntimeRow) => {
    setSelectedDefinition(row)
    setSelectedRowId(row.id)
    openInspector()
  }, [openInspector])
  const navigate = useCallback((item: ShellNavItem) => {
    setActiveItemId(item.id)
    setSelectedRowId(null)
    setSelectedDefinition(null)
  }, [])

  const authorizationClient = authorizationAdminClient
  if (authorizationClient === null) {
    return (
      <main className="happ-page" role="alert">
        <h1>Authorization admin is not configured</h1>
        <p>{authorizationAdminConfigError?.message ?? 'The authorization admin client failed to initialize.'}</p>
      </main>
    )
  }

  return (
    <AuthorizationAdminClientProvider client={authorizationClient}>
      {navigationError && <section role="alert"><p>{navigationError}</p><button type="button" onClick={() => setNavigationAttempt(value => value + 1)}>Retry navigation</button></section>}
      <AppShell
      key={packNavigation === null ? 'fallback' : 'configured'}
      shellId="harborline-app"
      brandText="Harborline"
      navigation={packNavigation ?? NAVIGATION}
      navigationState={NAVIGATION_STATE}
      resolveLabel={resolveLabel}
      roleVocabulary={roleVocabulary}
      heldRoles={EMPTY_HELD_ROLES}
      defaultActiveWorkspaceId={activeWorkspace?.id}
      activeItemId={activeItemId}
      onNavigate={navigate}
      onInspectorCommand={openInspector}
      pageHeader={
        <nav aria-label="Breadcrumb" className="happ-breadcrumb">
          Harborline / {contentWorkspaceLabel && `${contentWorkspaceLabel} / `}{body.title}
        </nav>
      }
      openPanelIds={openPanelIds}
      onOpenPanelIdsChange={setOpenPanelIds}
      panelToolbar={panel => panel.id === 'inspector' ? <p>{selectedDefinition ? `${displayTitle(selectedDefinition)} · follows selection` : 'No selection · follows selection'}</p> : null}
      panelContent={panel => panel.id === 'inspector'
        ? <section aria-label="Definition inspector">{selectedDefinition
          ? <><h2>{displayTitle(selectedDefinition)}</h2><pre>{JSON.stringify(selectedDefinition, null, 2)}</pre></>
          : <p>Select a Workshop definition to inspect it.</p>}</section>
        : <section className="happ-pilot"><p>{panel.id === 'pilot' ? `Pilot sees what you see — Portfolio · ${body.title}.` : `${resolveLabel(panel.labelKey ?? panel.id)}: This application surface is not available in this version.`}</p></section>}
      body={activeItemId === 'access.holders' ? <main className="happ-page"><AccessHoldersPage /></main>
        : activeItemId === CONFIGURATION_ITEM_ID
        ? <main className="happ-page"><ConfigurationActivationPage client={configurationActivationClient} /></main>
        : activeItemId === PROPOSAL_ITEM_ID
        ? <main className="happ-page"><ConfigurationProposalPage client={configurationProposalClient} proposalId={initialAddress.proposalId} /></main>
        : workshopItemIds(packNavigation).has(activeItemId)
        ? <main className="happ-page"><h1>{body.title}</h1>{initialAddress.surface === 'platform.editor.views' && activeItemId === 'views'
          ? <ViewAuthoringPage />
          : initialAddress.surface && !packNavigation
          ? <p role="status">Loading Workshop view…</p>
          : <SeededListPage itemId={activeItemId} viewId={initialAddress.surface && initialAddress.surface !== 'platform.editor.views' ? `platform.${initialAddress.surface}.${activeItemId}` : undefined} selectedRowId={selectedRowId} onRowActivate={inspectDefinition} onSelectionRestored={setSelectedDefinition} />}</main>
        : activeItemId === 'admin-authorization'
        ? <main className="happ-page"><AuthorizationAdminPage /></main>
        : <main className="happ-page"><h1>{body.title}</h1><p>{body.description}</p></main>}
    />
    </AuthorizationAdminClientProvider>
  )
}
