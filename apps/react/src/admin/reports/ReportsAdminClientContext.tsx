import { createContext, useContext } from 'react'
import type { PropsWithChildren } from 'react'
import type { ReportsAdminClient } from './client/types'

const ReportsAdminClientContext = createContext<ReportsAdminClient | null>(null)

export interface ReportsAdminClientProviderProps extends PropsWithChildren {
  readonly client: ReportsAdminClient
}

export function ReportsAdminClientProvider({ client, children }: ReportsAdminClientProviderProps) {
  return <ReportsAdminClientContext.Provider value={client}>{children}</ReportsAdminClientContext.Provider>
}

export function useReportsAdminClient(): ReportsAdminClient {
  const client = useContext(ReportsAdminClientContext)
  if (!client) throw new Error('ReportsAdminClientProvider is missing')
  return client
}
