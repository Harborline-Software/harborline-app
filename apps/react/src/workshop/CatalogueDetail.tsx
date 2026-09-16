import { useEffect, useState } from 'react'
import { SchemaForm, type ViewRuntimeRow } from '@harborline-software/ui-react'
import { formViewFromPlan } from './WorkshopWorkflow'
import { readSelectedCatalogue } from './selectedCatalogue'

const fields = ['formId', 'title', 'version', 'cascadeLayer'] as const
const detail = { id: 'platform.detail.form', version: '1.0.0' }
type ObjectValue = Readonly<Record<string, unknown>>
const object = (value: unknown): ObjectValue => value !== null && typeof value === 'object' && !Array.isArray(value) ? value as ObjectValue : {}
const canonicalVersion = (value: unknown): value is string => typeof value === 'string'
  && /^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$/.test(value)
  && value.split('.').every(part => Number(part) <= 2147483647)

async function request(path: string, signal: AbortSignal, body?: unknown): Promise<ObjectValue> {
  return object(await readSelectedCatalogue(`/api/local-node/catalogue/${path}`, signal, body))
}

export function detailRequests(definition: ObjectValue, source: ObjectValue) {
  const plan = object(definition.renderPlan)
  const mapping = object(object(definition.body).catalogueFieldSource)
  const declared = mapping.fields
  if (definition.id !== detail.id || definition.version !== detail.version || plan.definitionKind !== 'FormDefinition'
    || plan.definitionId !== detail.id || plan.definitionVersion !== detail.version
    || mapping.capabilityId !== 'forms.catalogue-field-source' || mapping.coordinateSchemaVersion !== 1
    || mapping.sourceMappingSchemaVersion !== 1 || mapping.sourceKind !== 'FormDefinition'
    || !Array.isArray(declared) || declared.length !== fields.length
    || declared.some((field, index) => object(field).fieldId !== fields[index] || object(field).source !== `catalogue.entry.${fields[index]}`)
    || typeof source.id !== 'string' || !canonicalVersion(source.version)
    || !source.catalogueFieldBinding) throw new Error('Unsupported detail.')
  return fields.map(field => ({ coordinate: { schemaVersion: 1, kind: mapping.sourceKind, id: source.id, version: source.version, field }, sourceBinding: source.catalogueFieldBinding }))
}

export function detailForm(definition: ObjectValue, response: ObjectValue) {
  const projection = object(response.projection)
  const plan = object(definition.renderPlan)
  const binding = object(projection.detailBinding)
  const provenance = object(binding.provenance)
  if (projection.detailId !== detail.id || projection.detailVersion !== detail.version || projection.readOnly !== true
    || binding.definitionHash !== `sha256:${plan.definitionHash}` || provenance.kind !== 'pack'
    || provenance.packKey !== plan.packKey || provenance.packVersion !== plan.packVersion) throw new Error('Unsupported projection.')
  const metadata = object(projection.fieldsMeta)
  const overlay = object(projection.overlay)
  const presentation = object(overlay.fields)
  const values = object(projection.values)
  if (metadata !== projection.fieldsMeta || overlay !== projection.overlay || presentation !== overlay.fields
    || values !== projection.values || !Array.isArray(overlay.sections)) throw new Error('Unsupported projection.')
  // Enumerate admitted metadata, never values: denied getters must remain untouched even
  // when a faulty transport supplies extra properties alongside an authorized projection.
  const admitted = Object.keys(metadata)
  if (admitted.some(key => !fields.includes(key as typeof fields[number]) || !Object.hasOwn(presentation, key) || !Object.hasOwn(values, key))) throw new Error('Unsupported projection.')
  const sections = Array.isArray(overlay.sections) ? overlay.sections.map(section => {
    const item = object(section)
    return { id: String(item.id), title: item.title, fields: Array.isArray(item.fields) ? item.fields.filter(key => typeof key === 'string' && admitted.includes(key)) : [] }
  }) : []
  const safeFields = Object.fromEntries(admitted.map(key => [key, presentation[key]]))
  const view = formViewFromPlan({ definitionId: detail.id, definitionVersion: detail.version, definitionKind: 'FormDefinition',
    bindings: { fields: metadata, overlay: { ...overlay, fields: safeFields, sections } } } as Parameters<typeof formViewFromPlan>[0])
  const renderedValues = Object.fromEntries(admitted.map(key => {
    const value = values[key]
    if (value === null || typeof value === 'string') return [key, value]
    const localized = object(value)
    const resolved = typeof localized.defaultLocale === 'string' ? object(localized.values)[localized.defaultLocale] : undefined
    if (key !== 'title' || typeof resolved !== 'string') throw new Error('Unsupported detail value.')
    return [key, resolved]
  }))
  return { view, values: renderedValues }
}

export function CatalogueDetail({ row }: { readonly row: ViewRuntimeRow }) {
  const [state, setState] = useState<{ row: ViewRuntimeRow; form: ReturnType<typeof detailForm> } | null>(null)
  useEffect(() => {
    const abort = new AbortController()
    setState(null)
    const source = object(row.catalogue)
    if (source.catalogueFieldBinding) void (async () => {
      const definition = await request(`definitions/FormDefinition/${detail.id}?version=${detail.version}`, abort.signal)
      const response = await request(`details/${detail.id}/${detail.version}`, abort.signal, detailRequests(definition, source))
      const form = detailForm(definition, response)
      if (!abort.signal.aborted) setState({ row, form })
    })().catch(() => { /* Missing, refused and unsupported details are absent. */ })
    return () => abort.abort()
  }, [row])
  return state?.row === row ? <SchemaForm view={state.form.view} values={state.form.values} readOnly onSubmit={() => undefined} /> : null
}
