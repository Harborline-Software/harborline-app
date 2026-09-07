import { createContext, useContext } from 'react'
import type { PropsWithChildren } from 'react'
import type { DataExchangeAdminClient } from './client/types'

const DataExchangeAdminClientContext = createContext<DataExchangeAdminClient | null>(null)

export interface DataExchangeAdminClientProviderProps extends PropsWithChildren {
  readonly client: DataExchangeAdminClient
}

export function DataExchangeAdminClientProvider({ client, children }: DataExchangeAdminClientProviderProps) {
  return <DataExchangeAdminClientContext.Provider value={client}>{children}</DataExchangeAdminClientContext.Provider>
}

export function useDataExchangeAdminClient(): DataExchangeAdminClient {
  const client = useContext(DataExchangeAdminClientContext)
  if (!client) throw new Error('DataExchangeAdminClientProvider is missing')
  return client
}
