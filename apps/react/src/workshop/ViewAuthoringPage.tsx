import { useState } from 'react'
import {
  ViewAuthoringEditor,
  emptyViewAuthoringDraft,
  type ViewAuthoringCatalogue,
} from '@harborline-software/ui-react'

const EMPTY_CATALOGUE: ViewAuthoringCatalogue = {
  recordTypes: [],
  viewKinds: [],
  fields: [],
  measures: [],
  widgets: [],
  rowActions: [],
}

export interface ViewAuthoringPageProps {
  readonly catalogue?: ViewAuthoringCatalogue
}

export function ViewAuthoringPage({ catalogue = EMPTY_CATALOGUE }: ViewAuthoringPageProps) {
  const [draft, setDraft] = useState(emptyViewAuthoringDraft)
  return <section aria-label="View authoring">
    <ViewAuthoringEditor value={draft} catalogue={catalogue} onChange={setDraft} />
  </section>
}
