import { useEffect, useState } from 'react'
import { readPackNavigation } from './navigation/packNavigation'
import { RoleVocabulary, type HeldRoleSet } from '@harborline-software/contracts/authorization'
import { AppShell, type PackNavigationDeclaration, type ShellNavigationState, type ShellNavItem } from '@harborline-software/ui-react'
import { FormsAdminPage } from './admin/forms/FormsAdminPage'
import { FormsAdminClientProvider } from './admin/forms/FormsAdminClientContext'
import { createFormsAdminClient, type FormsAdminClient } from './admin/forms/client'
import { ReportsAdminPage } from './admin/reports/ReportsAdminPage'
import { ReportsAdminClientProvider } from './admin/reports/ReportsAdminClientContext'
import { createReportsAdminClient } from './admin/reports/client'
import { ViewsAdminPage } from './admin/views/ViewsAdminPage'
import { ViewsAdminClientProvider } from './admin/views/ViewsAdminClientContext'
import { createViewsAdminClient } from './admin/views/client'
import { DataExchangeAdminPage } from './admin/data-exchange/DataExchangeAdminPage'
import { DataExchangeAdminClientProvider } from './admin/data-exchange/DataExchangeAdminClientContext'
import { createDataExchangeAdminClient } from './admin/data-exchange/client'
import { SchedulingAdminPage } from './admin/scheduling/SchedulingAdminPage'
import { SchedulingAdminClientProvider } from './admin/scheduling/SchedulingAdminClientContext'
import { createSchedulingAdminClient } from './admin/scheduling/client'
import { AuthorizationAdminPage } from './admin/authorization/AuthorizationAdminPage'
import { AuthorizationAdminClientProvider } from './admin/authorization/AuthorizationAdminClientContext'
import { createAuthorizationAdminClient, type AuthorizationAdminClient } from './admin/authorization/client'

// Composed from the platform's hlp.ui.app-shell module rather than hand-written chrome, and kept
// deliberately identical to apps/blazor/Shell.razor: same workspace, same item ids, same labels,
// same activation-driven (not href-driven) navigation, same body response to selection, same
// Pilot-panel placeholder behind the header sparkle action. These two files are one application
// in two lanes; when they disagree, one of them is wrong.
// Ticket 153 review: createFormsAdminClient THROWS when neither VITE_FORMS_API_ORIGIN nor
// VITE_FORMS_FIXTURE is set. Uncaught at module scope, that throw fired during bundle
// evaluation — BEFORE React mounted — leaving a white page and a console-only error. The
// failure stays loud but is deferred into the render path: App renders a visible
// configuration-error screen instead of never mounting.
let formsAdminClient: FormsAdminClient | null = null
let formsAdminConfigError: Error | null = null
try {
  formsAdminClient = createFormsAdminClient()
} catch (error) {
  formsAdminConfigError = error instanceof Error ? error : new Error(String(error))
}
const reportsAdminClient = createReportsAdminClient()
const viewsAdminClient = createViewsAdminClient()
const dataExchangeAdminClient = createDataExchangeAdminClient()
const schedulingAdminClient = createSchedulingAdminClient()
let authorizationAdminClient: AuthorizationAdminClient | null = null
let authorizationAdminConfigError: Error | null = null
try {
  authorizationAdminClient = createAuthorizationAdminClient()
} catch (error) {
  authorizationAdminConfigError = error instanceof Error ? error : new Error(String(error))
}

const EMPTY_ROLE_VOCABULARY = RoleVocabulary.fromApi([])
const EMPTY_HELD_ROLES: HeldRoleSet = { roles: [] }

const NAV_ITEMS: readonly ShellNavItem[] = [
  { id: 'assets', label: 'Assets' },
  { id: 'run-report', label: 'Run report' },
  { id: 'admin-forms', label: 'Forms' },
  { id: 'admin-reports', label: 'Reports' },
  { id: 'admin-views', label: 'Views' },
  { id: 'admin-data-exchange', label: 'Data Exchange' },
  { id: 'admin-scheduling', label: 'Scheduling' },
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
const NAVIGATION_STATE: ShellNavigationState = {
  items: Object.fromEntries(NAV_ITEMS.map(item => [item.id, item])),
}

const BODY: Record<string, { title: string; description: string }> = {
  assets: {
    title: 'Assets',
    description: 'Browse and manage the physical assets your organization tracks.',
  },
  'run-report': {
    title: 'Run report',
    description: 'Generate a report across the assets your organization tracks.',
  },
  'admin-forms': {
    title: 'Forms',
    description: 'Administer form definitions and version history for this tenant.',
  },
  'admin-reports': {
    title: 'Reports',
    description: 'Administer report definitions and version history for this tenant.',
  },
  'admin-views': {
    title: 'Views',
    description: 'Administer view definitions and version history for this tenant.',
  },
  'admin-data-exchange': {
    title: 'Data Exchange',
    description: 'Administer data-exchange definitions and version history for this tenant.',
  },
  'admin-scheduling': {
    title: 'Scheduling',
    description: 'Administer scheduling definitions and revision history for this tenant.',
  },
  'admin-authorization': {
    title: 'Settings › System',
    description: 'Administer capability-role bindings and inspect the standing catalogue.',
  },
}

export function App() {
  const [activeItemId, setActiveItemId] = useState('assets')
  const [openPanelIds, setOpenPanelIds] = useState<readonly string[]>([])
  const [packNavigation, setPackNavigation] = useState<PackNavigationDeclaration | null>(null)
  const [navigationError, setNavigationError] = useState<string | null>(null)
  const [navigationAttempt, setNavigationAttempt] = useState(0)
  const [roleVocabulary, setRoleVocabulary] = useState(EMPTY_ROLE_VOCABULARY)
  const body = BODY[activeItemId] ?? { title: activeItemId, description: 'This application surface is not available in this version.' }

  useEffect(() => {
    if (authorizationAdminClient === null || formsAdminClient === null) return
    const abort = new AbortController()
    setNavigationError(null)
    void readPackNavigation(abort.signal).then(declaration => {
      if (abort.signal.aborted) return
      setPackNavigation(declaration)
      setOpenPanelIds((declaration ?? NAVIGATION).panelSet?.filter(panel => panel.defaultOpen).map(panel => panel.id) ?? [])
      setActiveItemId('assets')
    }).catch((error: unknown) => {
      if (!abort.signal.aborted) setNavigationError(error instanceof Error ? error.message : 'Unable to load application navigation.')
    })
    return () => abort.abort()
  }, [navigationAttempt])

  useEffect(() => {
    const abort = new AbortController()
    if (authorizationAdminClient !== null) {
      void authorizationAdminClient.listRoleVocabulary(abort.signal)
        .then(definitions => setRoleVocabulary(RoleVocabulary.fromApi(definitions)))
        .catch(() => setRoleVocabulary(EMPTY_ROLE_VOCABULARY))
    }
    return () => abort.abort()
  }, [])

  // The constant-per-bundle configuration failure, rendered ON SCREEN (never a white page).
  const client = formsAdminClient
  if (client === null) {
    return (
      <main className="happ-page" role="alert">
        <h1>Forms admin is not configured</h1>
        <p>{formsAdminConfigError?.message ?? 'The forms admin client failed to initialize.'}</p>
      </main>
    )
  }
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
    <FormsAdminClientProvider client={client}>
      <ReportsAdminClientProvider client={reportsAdminClient}>
      <ViewsAdminClientProvider client={viewsAdminClient}>
      <DataExchangeAdminClientProvider client={dataExchangeAdminClient}>
      <SchedulingAdminClientProvider client={schedulingAdminClient}>
      <AuthorizationAdminClientProvider client={authorizationClient}>
      {navigationError && <section role="alert"><p>{navigationError}</p><button type="button" onClick={() => setNavigationAttempt(value => value + 1)}>Retry navigation</button></section>}
      <AppShell
      shellId="harborline-app"
      brandText="Harborline"
      navigation={packNavigation ?? NAVIGATION}
      navigationState={NAVIGATION_STATE}
      roleVocabulary={roleVocabulary}
      heldRoles={EMPTY_HELD_ROLES}
      activeItemId={activeItemId}
      onNavigate={(item: ShellNavItem) => setActiveItemId(item.id)}
      pageHeader={
        <nav aria-label="Breadcrumb" className="happ-breadcrumb">
          Harborline / Portfolio / {body.title}
        </nav>
      }
      openPanelIds={openPanelIds}
      onOpenPanelIdsChange={setOpenPanelIds}
      panelContent={panel => <section className="happ-pilot"><p>{panel.id === 'pilot' ? `Pilot sees what you see — Portfolio · ${body.title}.` : `${panel.labelKey ?? panel.id}: This application surface is not available in this version.`}</p></section>}
      body={activeItemId === 'admin-forms'
        ? <main className="happ-page"><FormsAdminPage /></main>
        : activeItemId === 'admin-reports'
          ? <main className="happ-page"><ReportsAdminPage /></main>
          : activeItemId === 'admin-views'
            ? <main className="happ-page"><ViewsAdminPage /></main>
            : activeItemId === 'admin-data-exchange'
              ? <main className="happ-page"><DataExchangeAdminPage /></main>
              : activeItemId === 'admin-scheduling'
              ? <main className="happ-page"><SchedulingAdminPage /></main>
                : activeItemId === 'admin-authorization'
                  ? <main className="happ-page"><AuthorizationAdminPage /></main>
                  : <main className="happ-page"><h1>{body.title}</h1><p>{body.description}</p></main>}
    />
      </AuthorizationAdminClientProvider>
      </SchedulingAdminClientProvider>
      </DataExchangeAdminClientProvider>
      </ViewsAdminClientProvider>
      </ReportsAdminClientProvider>
    </FormsAdminClientProvider>
  )
}
