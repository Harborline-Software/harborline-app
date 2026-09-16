import type { SelectedSessionTransport } from './selected-session-transport.mjs'
export interface PackAction { id: string; label: string; fileInput?: { accept: string } | null }
export interface PackFormPlan {
  definitionId: string; definitionVersion: string; definitionKind: string;
  bindings: { fields?: Record<string, { type?: string; required?: boolean; options?: readonly string[] | null }>; overlay?: Record<string, never> }
}
export interface PackReceipt { status: number; code: string | null; body: unknown; text: string; auditId: string | null; correlationId: string | null }
export interface PackRuntimeState {
  plan: unknown; rows: readonly { id: string; values: Record<string, unknown> }[];
  actions: readonly PackAction[]; selectedId: string | null; activeAction: PackAction | null;
  inputPlan: PackFormPlan | null; receipt: PackReceipt | null; error: string | null; busy: boolean
}
export interface PackActionRuntime {
  snapshot(): PackRuntimeState
  load(viewId: string): Promise<PackRuntimeState>
  select(id: string): PackRuntimeState
  begin(id: string): Promise<PackRuntimeState>
  invoke(values?: Readonly<Record<string, unknown>>, file?: Blob | Uint8Array): Promise<PackRuntimeState>
}
export function createPackActionRuntime(transport?: SelectedSessionTransport, uuid?: () => string): PackActionRuntime
