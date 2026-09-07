import { createContext, useContext } from 'react'
import type { PropsWithChildren } from 'react'
import type { ViewsAdminClient } from './client/types'

const ViewsAdminClientContext = createContext<ViewsAdminClient | null>(null)

export interface ViewsAdminClientProviderProps extends PropsWithChildren {
  readonly client: ViewsAdminClient
}

export function ViewsAdminClientProvider({ client, children }: ViewsAdminClientProviderProps) {
  return <ViewsAdminClientContext.Provider value={client}>{children}</ViewsAdminClientContext.Provider>
}

export function useViewsAdminClient(): ViewsAdminClient {
  const client = useContext(ViewsAdminClientContext)
  if (!client) throw new Error('ViewsAdminClientProvider is missing')
  return client
}
