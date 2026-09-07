import { createContext, useContext } from 'react'
import type { PropsWithChildren } from 'react'
import type { FormsAdminClient } from './client/types'

const FormsAdminClientContext = createContext<FormsAdminClient | null>(null)

export interface FormsAdminClientProviderProps extends PropsWithChildren {
  readonly client: FormsAdminClient
}

export function FormsAdminClientProvider({ client, children }: FormsAdminClientProviderProps) {
  return <FormsAdminClientContext.Provider value={client}>{children}</FormsAdminClientContext.Provider>
}

export function useFormsAdminClient(): FormsAdminClient {
  const client = useContext(FormsAdminClientContext)
  if (!client) throw new Error('FormsAdminClientProvider is missing')
  return client
}
