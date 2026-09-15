import { useEffect, useMemo, useRef, useState, type ComponentProps } from 'react'
import {
  SchemaForm,
  ViewRuntime,
  type ViewRenderPlan,
  type ViewRuntimeRow,
} from '@harborline-software/ui-react'

type FormView = ComponentProps<typeof SchemaForm>['view']
type ValidationResult = Exclude<Awaited<ReturnType<ComponentProps<typeof SchemaForm>['onSubmit']>>, void>

interface WorkshopAction {
  readonly id: string
  readonly label: string
  readonly operation: string
  readonly inputForm?: string
  readonly input?: string
}

interface WorkshopPlan extends ViewRenderPlan {
  readonly bindings: ViewRenderPlan['bindings'] & {
    readonly actions?: readonly { readonly id: string; readonly label: string }[]
    readonly parameters?: NonNullable<ViewRenderPlan['bindings']['parameters']> & {
      readonly actions?: readonly WorkshopAction[]
      readonly entityType?: string
    }
  }
}

interface FormRenderPlan {
  readonly definitionId: string
  readonly definitionVersion: string
  readonly definitionKind: string
  readonly bindings: {
    readonly fields?: Readonly<Record<string, FieldBinding>>
    readonly overlay?: FormOverlay
  }
}

interface FieldBinding {
  readonly type?: string
  readonly required?: boolean
  readonly options?: readonly string[] | null
}

interface LiteralText {
  readonly kind?: string
  readonly value?: string
  readonly defaultLocale?: string
  readonly values?: Readonly<Record<string, string>>
}

interface FieldOverlay {
  readonly label?: LiteralText | string
  readonly helpText?: LiteralText | string
  readonly controlHint?: string
  readonly piiSensitivity?: string
  readonly config?: Readonly<Record<string, unknown>>
}

interface FormOverlay {
  readonly title?: LiteralText | string
  readonly description?: LiteralText | string
  readonly fields?: Readonly<Record<string, FieldOverlay>>
  readonly sections?: readonly {
    readonly id: string
    readonly title?: LiteralText | string
    readonly fields?: readonly string[]
  }[]
}

interface FormCatalogueEntry {
  readonly id: string
  readonly version: string
  readonly renderPlan?: FormRenderPlan | null
}

interface PackContent {
  readonly key: string
  readonly kind: string
  readonly version: string
  readonly content?: Readonly<Record<string, unknown>>
}

interface PackCandidate extends Readonly<Record<string, unknown>> {
  readonly key: string
  readonly version: string
  readonly contents: readonly PackContent[]
}

interface PropertyFormBinding { readonly definition: string; readonly version: string }
interface AssetTypeDetail {
  readonly id: string
  readonly displayName: string
  readonly propertyForm?: PropertyFormBinding | null
}

interface EntityReceipt { readonly id?: string; readonly auditId?: string }

interface WorkflowState {
  readonly candidate?: PackCandidate
  readonly validation?: Readonly<Record<string, unknown>>
  readonly artifact?: Blob
  readonly download?: { readonly href: string; readonly name: string }
  readonly verification?: Readonly<Record<string, unknown>>
  readonly check?: Readonly<Record<string, unknown>>
  readonly installation?: Readonly<Record<string, unknown>>
  readonly activation?: Readonly<Record<string, unknown>>
  readonly assetType?: AssetTypeDetail
  readonly assetContent?: PackContent
  readonly propertyForm?: FormCatalogueEntry
  readonly receipt?: EntityReceipt
}

interface ActiveForm {
  readonly action: WorkshopAction
  readonly kind: 'pack' | 'record'
  readonly entry: FormCatalogueEntry
}

interface ResultView { readonly key: number; readonly label: string; readonly text: string }

const checkCollections = [
  'conflicts', 'watermarkHits', 'admissionRefusals', 'refusalCodes', 'refusals',
  'crossPackCollisions', 'unmetContentReferences', 'unmetDependencies',
] as const

function checkPassed(value: unknown): boolean {
  const result = object(value)
  return (result?.verdict === 'WouldInstall' || result?.verdict === 'WouldUpgrade')
    && checkCollections.every(key => Array.isArray(result[key]) && result[key].length === 0)
}

interface ResponseBody {
  readonly value: unknown
  readonly text: string
}

class ServerResponseError extends Error {
  constructor(readonly status: number, readonly body: ResponseBody) {
    super(body.text || `Request failed (${status}).`)
  }
}

const origin = () => import.meta.env.DEV ? '' : (import.meta.env.VITE_FORMS_API_ORIGIN?.replace(/\/$/, '') ?? '')

async function responseBody(response: Response): Promise<ResponseBody> {
  const text = await response.text()
  if (!text) return { value: null, text: '' }
  try { return { value: JSON.parse(text) as unknown, text } } catch { return { value: text, text } }
}

async function requestJson(path: string, init?: RequestInit): Promise<ResponseBody> {
  const response = await fetch(`${origin()}${path}`, { credentials: 'include', ...init })
  const body = await responseBody(response)
  if (!response.ok) throw new ServerResponseError(response.status, body)
  return body
}

function pretty(value: unknown, fallback: string): string {
  if (typeof value === 'string') return value
  if (value === undefined) return fallback
  return JSON.stringify(value, null, 2)
}

function localized(value: LiteralText | string | undefined, fallback: string) {
  if (typeof value === 'string') return { defaultLocale: 'en', values: { en: value } }
  if (value?.values && value.defaultLocale) return { defaultLocale: value.defaultLocale, values: value.values }
  const text = value?.kind === 'Literal' && typeof value.value === 'string' ? value.value : fallback
  return { defaultLocale: 'en', values: { en: text } }
}

function valueKind(type: string | undefined): string {
  if (type === 'checkbox') return 'boolean'
  if (type === 'number' || type === 'currency') return 'number'
  return 'string'
}

export function formViewFromPlan(plan: FormRenderPlan): FormView {
  if (plan.definitionKind !== 'FormDefinition') throw new Error('The declared input has no FormDefinition render plan.')
  const fields = plan.bindings.fields ?? {}
  const overlay = plan.bindings.overlay ?? {}
  const field = (name: string) => {
    const binding = fields[name] ?? {}
    const presentation = overlay.fields?.[name] ?? {}
    return {
      name,
      label: localized(presentation.label, name),
      ...(presentation.helpText ? { helpText: localized(presentation.helpText, name) } : {}),
      controlHint: presentation.controlHint ?? binding.type ?? 'text',
      isSensitive: presentation.piiSensitivity === 'Sensitive',
      isReadable: true,
      valueKind: valueKind(binding.type),
      required: binding.required === true,
      options: binding.options?.map(option => ({ value: option, label: option })),
      config: presentation.config ?? null,
    }
  }
  const sections = overlay.sections?.length
    ? overlay.sections.map(section => ({
        id: section.id,
        title: localized(section.title, section.id),
        fields: (section.fields ?? []).filter(name => fields[name] !== undefined).map(field),
      }))
    : [{ id: 'main', title: localized(undefined, 'Details'), fields: Object.keys(fields).map(field) }]
  return {
    formId: plan.definitionId,
    version: plan.definitionVersion,
    title: localized(overlay.title, plan.definitionId),
    description: localized(overlay.description, ''),
    sections,
  }
}

function object(value: unknown): Readonly<Record<string, unknown>> | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as Readonly<Record<string, unknown>>
    : null
}

function proof(value: unknown, name: string): boolean {
  return object(value)?.[name] === true
}

function strings(value: unknown, name: string): readonly unknown[] {
  const candidate = object(value)?.[name]
  return Array.isArray(candidate) ? candidate : []
}

function validationErrors(value: unknown, defaultPointer: string): ValidationResult {
  const body = object(value)
  const pointerList = Array.isArray(body?.pointers) ? body.pointers.filter(pointer => typeof pointer === 'string') as string[] : []
  if (pointerList.length > 0) {
    const message = typeof body?.code === 'string' ? body.code : pretty(value, 'Request refused.')
    return {
      isValid: false,
      errors: pointerList.map(raw => ({
        jsonPointer: raw === '/values' ? '' : raw.startsWith('/values/') ? raw.slice('/values'.length) : raw,
        message,
        kind: 'Schema' as const,
        ...(typeof body?.code === 'string' ? { code: body.code } : {}),
      })),
    }
  }
  const errors = Array.isArray(body?.errors) ? body.errors : []
  if (errors.length > 0) {
    return {
      isValid: false,
      errors: errors.map(error => {
        const item = object(error)
        const raw = typeof item?.jsonPointer === 'string' ? item.jsonPointer
          : typeof item?.pointer === 'string' ? item.pointer : defaultPointer
        return {
          jsonPointer: raw === '/values' ? '' : raw.startsWith('/values/') ? raw.slice('/values'.length) : raw,
          message: typeof item?.message === 'string' ? item.message : pretty(item, 'Request refused.'),
          kind: 'Schema' as const,
          ...(typeof item?.code === 'string' ? { code: item.code } : {}),
        }
      }),
    }
  }
  const codes = Array.isArray(body?.codes) ? body.codes : []
  return {
    isValid: false,
    errors: [{
      jsonPointer: defaultPointer,
      message: codes.length > 0 ? codes.map(code => {
        const item = object(code)
        return [item?.code, item?.target].filter(part => typeof part === 'string').join(' ')
      }).join('; ') : pretty(value, 'Request refused.'),
      kind: 'Schema',
    }],
  }
}

function firstNonemptyString(value: unknown): string | undefined {
  if (typeof value === 'string') return value.trim().length > 0 ? value : undefined
  if (Array.isArray(value)) {
    for (const item of value) {
      const found = firstNonemptyString(item)
      if (found) return found
    }
    return undefined
  }
  const record = object(value)
  if (!record) return undefined
  for (const item of Object.values(record)) {
    const found = firstNonemptyString(item)
    if (found) return found
  }
  return undefined
}

function fileName(response: Response, candidate: PackCandidate): string {
  const disposition = response.headers.get('content-disposition')
  const match = disposition?.match(/filename="?([^";]+)"?/i)
  return match?.[1] ?? `${candidate.key}-${candidate.version}.pack`
}

export function WorkshopWorkflow({ plan, rows, onRowActivate, onActivated }: {
  readonly plan: ViewRenderPlan
  readonly rows: readonly ViewRuntimeRow[]
  readonly onRowActivate: (rowId: string) => void
  readonly onActivated: () => Promise<void>
}) {
  const workshopPlan = plan as WorkshopPlan
  const actions = workshopPlan.bindings.parameters?.actions ?? []
  const [workflow, setWorkflow] = useState<WorkflowState>({})
  const [activeForm, setActiveForm] = useState<ActiveForm | null>(null)
  const [authorValues, setAuthorValues] = useState<Readonly<Record<string, unknown>>>({})
  const [results, setResults] = useState<readonly ResultView[]>([])
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const busyRef = useRef(false)
  const lifetime = useRef(new AbortController())
  const formView = useMemo(() => activeForm?.entry.renderPlan ? formViewFromPlan(activeForm.entry.renderPlan) : null, [activeForm])

  useEffect(() => {
    const controller = new AbortController()
    lifetime.current = controller
    return () => controller.abort()
  }, [])
  useEffect(() => () => { if (workflow.download) URL.revokeObjectURL(workflow.download.href) }, [workflow.download])

  const append = (action: WorkshopAction, value: unknown, fallback = 'Completed.') => {
    setResults(current => [...current, { key: Date.now() + current.length, label: action.label, text: pretty(value, fallback) }])
  }
  const fail = (action: WorkshopAction, reason: unknown) => {
    const text = reason instanceof ServerResponseError
      ? (reason.body.text || reason.message)
      : reason instanceof Error ? reason.message : 'The command could not be completed.'
    if (reason instanceof ServerResponseError) append(action, reason.body.value, text)
    setMessage(text)
  }
  const requires = (condition: boolean, prerequisite: string | undefined) => {
    if (condition) return true
    setMessage(prerequisite ? `Complete “${prerequisite}” first.` : 'The command prerequisite is not available.')
    return false
  }
  const label = (operation: string) => actions.find(action => action.operation === operation)?.label

  const loadForm = async (action: WorkshopAction, id: string, version?: string) => {
    const query = version ? `?version=${encodeURIComponent(version)}` : ''
    const body = await requestJson(`/api/local-node/catalogue/definitions/FormDefinition/${encodeURIComponent(id)}${query}`, { signal: lifetime.current.signal })
    const entry = object(body.value) as FormCatalogueEntry | null
    if (!entry?.renderPlan) throw new Error('The declared input form has no active compiled render plan.')
    setActiveForm({ action, kind: action.operation === 'pack.validate' ? 'pack' : 'record', entry })
  }

  const activateProjection = async (action: WorkshopAction, candidate: PackCandidate, activation: Readonly<Record<string, unknown>>) => {
    const assetTypes = candidate.contents.filter(content => content.kind === 'AssetTypeDefinition')
    if (assetTypes.length !== 1) throw new Error('The candidate must declare exactly one AssetTypeDefinition.')
    if (strings(activation, 'projectionRefusals').length !== 0 || strings(activation, 'platformRefusals').length !== 0) {
      throw new Error('Activation completed with projection refusals; the record form is unavailable.')
    }
    const asset = assetTypes[0]
    const detailBody = await requestJson(`/api/local-node/asset-registry/types/${encodeURIComponent(asset.key)}`, { signal: lifetime.current.signal })
    const detail = object(detailBody.value) as AssetTypeDetail | null
    if (!detail?.propertyForm?.definition || !detail.propertyForm.version) throw new Error('The active record type has no pinned property form.')
    const formBody = await requestJson(`/api/local-node/catalogue/definitions/FormDefinition/${encodeURIComponent(detail.propertyForm.definition)}?version=${encodeURIComponent(detail.propertyForm.version)}`, { signal: lifetime.current.signal })
    const propertyForm = object(formBody.value) as FormCatalogueEntry | null
    if (!propertyForm?.renderPlan) throw new Error('The active property form has no compiled render plan.')
    setWorkflow(current => ({ ...current, activation, assetType: detail, assetContent: asset, propertyForm }))
    await onActivated()
  }

  const dispatch = async (actionId: string) => {
    if (busyRef.current) return
    const action = actions.find(candidate => candidate.id === actionId)
    if (!action) { setMessage('The selected command is not declared by the active view.'); return }
    setMessage(null)
    busyRef.current = true
    setBusy(true)
    try {
      switch (action.operation) {
        case 'pack.validate':
          if (!action.inputForm) throw new Error('The validation command has no declared input form.')
          await loadForm(action, action.inputForm)
          break
        case 'pack.export': {
          if (!requires(proof(workflow.validation, 'valid') && Boolean(workflow.candidate), label('pack.validate'))) break
          const response = await fetch(`${origin()}/api/local-node/packs/export`, {
            method: 'POST', credentials: 'include', headers: { 'content-type': 'application/json' },
            body: JSON.stringify(workflow.candidate), signal: lifetime.current.signal,
          })
          if (!response.ok) throw new ServerResponseError(response.status, await responseBody(response))
          const artifact = await response.blob()
          const download = { href: URL.createObjectURL(artifact), name: fileName(response, workflow.candidate!) }
          setWorkflow(current => ({ candidate: current.candidate, validation: current.validation, artifact, download }))
          append(action, { fileName: download.name, size: artifact.size, type: artifact.type })
          break
        }
        case 'pack.verify': {
          if (!requires(Boolean(workflow.artifact), label('pack.export'))) break
          setWorkflow(current => ({ ...current, verification: undefined, check: undefined, installation: undefined, activation: undefined, assetType: undefined, assetContent: undefined, propertyForm: undefined, receipt: undefined }))
          const body = await requestJson('/api/local-node/packs/verify', { method: 'POST', headers: { 'content-type': 'application/octet-stream' }, body: workflow.artifact, signal: lifetime.current.signal })
          const verification = object(body.value) ?? {}
          setWorkflow(current => ({ ...current, verification, check: undefined, installation: undefined, activation: undefined, assetType: undefined, propertyForm: undefined, receipt: undefined }))
          append(action, body.value)
          break
        }
        case 'pack.check': {
          if (!requires(object(workflow.verification)?.verdict === 'Verified' && Boolean(workflow.artifact), label('pack.verify'))) break
          setWorkflow(current => ({ ...current, check: undefined, installation: undefined, activation: undefined, assetType: undefined, assetContent: undefined, propertyForm: undefined, receipt: undefined }))
          const body = await requestJson('/api/local-node/packs/preview', { method: 'POST', headers: { 'content-type': 'application/octet-stream' }, body: workflow.artifact, signal: lifetime.current.signal })
          const check = object(body.value) ?? {}
          setWorkflow(current => ({ ...current, check, installation: undefined, activation: undefined, assetType: undefined, assetContent: undefined, propertyForm: undefined, receipt: undefined }))
          append(action, body.value)
          if (!checkPassed(check)) setMessage('Pack check refused this candidate. Review every reported code and pointer before installing it.')
          break
        }
        case 'pack.install': {
          const checkLabel = label('pack.check')
          if (!requires(Boolean(workflow.artifact) && (checkLabel ? checkPassed(workflow.check) : object(workflow.verification)?.verdict === 'Verified'), checkLabel ?? label('pack.verify'))) break
          setWorkflow(current => ({ ...current, installation: undefined, activation: undefined, assetType: undefined, assetContent: undefined, propertyForm: undefined, receipt: undefined }))
          const body = await requestJson('/api/local-node/packs/install', { method: 'POST', headers: { 'content-type': 'application/octet-stream' }, body: workflow.artifact, signal: lifetime.current.signal })
          const installation = object(body.value) ?? {}
          setWorkflow(current => ({ ...current, installation, activation: undefined, assetType: undefined, propertyForm: undefined, receipt: undefined }))
          append(action, body.value)
          break
        }
        case 'pack.activate': {
          if (!requires(proof(workflow.installation, 'installed') && Boolean(workflow.candidate), label('pack.install'))) break
          const candidate = workflow.candidate!
          setWorkflow(current => ({ ...current, activation: undefined, assetType: undefined, assetContent: undefined, propertyForm: undefined, receipt: undefined }))
          const body = await requestJson('/api/local-node/packs/activate', {
            method: 'POST', headers: { 'content-type': 'application/json' },
            body: JSON.stringify({ packKey: candidate.key, version: candidate.version }), signal: lifetime.current.signal,
          })
          const activation = object(body.value) ?? {}
          append(action, body.value)
          if (!proof(activation, 'activated')) break
          await activateProjection(action, candidate, activation)
          break
        }
        case 'record.create':
          if (action.input !== 'active-pack.property-form') throw new Error('The record command has no supported declared input binding.')
          if (!requires(proof(workflow.activation, 'activated') && Boolean(workflow.propertyForm), label('pack.activate'))) break
          setActiveForm({ action, kind: 'record', entry: workflow.propertyForm! })
          break
        case 'record.read': {
          if (!requires(Boolean(workflow.receipt?.id && workflow.receipt.auditId), label('record.create'))) break
          const [entity, trace] = await Promise.allSettled([
            requestJson(`/api/local-node/asset-registry/entities/${encodeURIComponent(workflow.receipt!.id!)}`, { signal: lifetime.current.signal }),
            requestJson(`/api/local-node/authorization/traces/${encodeURIComponent(workflow.receipt!.auditId!)}`, { signal: lifetime.current.signal }),
          ])
          const evidence = {
            entity: entity.status === 'fulfilled' ? entity.value.value : entity.reason instanceof ServerResponseError ? entity.reason.body.value : String(entity.reason),
            trace: trace.status === 'fulfilled' ? trace.value.value : trace.reason instanceof ServerResponseError ? trace.reason.body.value : String(trace.reason),
          }
          append(action, evidence)
          if (entity.status === 'rejected') throw entity.reason
          if (trace.status === 'rejected') throw trace.reason
          break
        }
        default:
          setMessage('The selected command is not declared by the Workshop host.')
      }
    } catch (reason) { fail(action, reason) } finally { busyRef.current = false; setBusy(false) }
  }

  const submitPack = async (values: Readonly<Record<string, unknown>>): Promise<ValidationResult | void> => {
    const action = activeForm!.action
    if (busyRef.current) return validationErrors(null, '/packJson')
    busyRef.current = true
    setBusy(true)
    try {
      setAuthorValues(values)
      setMessage(null)
      setWorkflow({})
      if (typeof values.packJson !== 'string') throw new SyntaxError('Enter a JSON pack document.')
      const parsed = JSON.parse(values.packJson) as unknown
      const document = object(parsed)
      if (!document || typeof document.key !== 'string' || typeof document.version !== 'string' || !Array.isArray(document.contents)) {
        throw new SyntaxError('The pack document must include key, version, and contents.')
      }
      const candidate = document as PackCandidate
      const body = await requestJson('/api/local-node/packs/export?validateOnly=true', {
        method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(candidate), signal: lifetime.current.signal,
      })
      append(action, body.value)
      if (!proof(body.value, 'valid')) return validationErrors(body.value, '/packJson')
      setWorkflow({ candidate, validation: object(body.value) ?? {} })
      return { isValid: true, errors: [] }
    } catch (reason) {
      if (reason instanceof SyntaxError) {
        return { isValid: false, errors: [{ jsonPointer: '/packJson', message: reason.message, kind: 'Schema' }] }
      }
      fail(action, reason)
      return validationErrors(reason instanceof ServerResponseError ? reason.body.value : null, '/packJson')
    } finally { busyRef.current = false; setBusy(false) }
  }

  const submitRecord = async (values: Readonly<Record<string, unknown>>): Promise<ValidationResult | void> => {
    const action = activeForm!.action
    if (busyRef.current) return validationErrors(null, '')
    busyRef.current = true
    setBusy(true)
    try {
      if (!proof(workflow.activation, 'activated')
        || !workflow.propertyForm?.renderPlan
        || workflow.propertyForm.id !== activeForm?.entry.id
        || workflow.propertyForm.version !== activeForm.entry.version) {
        setMessage('Activate a bound record type before submitting a record.')
        return validationErrors(null, '')
      }
      const type = workflow.assetContent?.key
      if (!type) return validationErrors(null, '')
      const firstText = firstNonemptyString(values)
      const candidateName = object(workflow.assetContent?.content)?.displayName
      const displayName = firstText ?? (typeof candidateName === 'string' ? candidateName : workflow.assetType?.displayName)
      if (!displayName) return validationErrors(null, '')
      setMessage(null)
      setWorkflow(current => ({ ...current, receipt: undefined }))
      const body = await requestJson('/api/local-node/asset-registry/entities', {
        method: 'POST', headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ type, displayName, values }), signal: lifetime.current.signal,
      })
      const receipt = object(body.value) as EntityReceipt | null
      append(action, body.value)
      if (typeof receipt?.id !== 'string' || receipt.id.trim().length === 0
        || typeof receipt.auditId !== 'string' || receipt.auditId.trim().length === 0) {
        return validationErrors(body.value, '')
      }
      setWorkflow(current => ({ ...current, receipt }))
      return { isValid: true, errors: [] }
    } catch (reason) {
      fail(action, reason)
      return validationErrors(reason instanceof ServerResponseError ? reason.body.value : null, '')
    } finally { busyRef.current = false; setBusy(false) }
  }

  const invalidateAuthoredPack = () => {
    if (!workflow.candidate && !workflow.artifact) return
    setWorkflow({})
    setResults([])
    setMessage(null)
  }

  const changeAuthorValues = (values: Readonly<Record<string, unknown>>) => {
    setAuthorValues(values)
    invalidateAuthoredPack()
  }

  return <>
    <ViewRuntime
      plan={workshopPlan}
      rows={rows}
      empty="No definitions."
      actionsDisabled={busy}
      onRowActivate={onRowActivate}
      onAction={actionId => { void dispatch(actionId) }}
    />
    {busy && <p role="status">Working…</p>}
    {message && <section role="alert"><pre>{message}</pre></section>}
    {activeForm && formView && <section aria-label={activeForm.action.label}>
      <SchemaForm
        key={`${activeForm.kind}:${activeForm.entry.id}:${activeForm.entry.version}`}
        view={formView}
        values={activeForm.kind === 'pack' ? authorValues : undefined}
        onValuesChange={activeForm.kind === 'pack' ? changeAuthorValues : undefined}
        onSubmit={activeForm.kind === 'pack' ? submitPack : submitRecord}
        strings={{ submit: activeForm.action.label, submitting: activeForm.action.label }}
      />
    </section>}
    {workflow.download && <p><a href={workflow.download.href} download={workflow.download.name}>{workflow.download.name}</a></p>}
    {results.length > 0 && <section aria-label="Workshop command results">
      {results.map(result => <article key={result.key}><h2>{result.label}</h2><pre>{result.text}</pre></article>)}
    </section>}
  </>
}
