import { createContext, useContext } from 'react'
import type { PropsWithChildren } from 'react'
import type { SchedulingAdminClient } from './client/types'

const SchedulingAdminClientContext = createContext<SchedulingAdminClient | null>(null)

export interface SchedulingAdminClientProviderProps extends PropsWithChildren {
  readonly client: SchedulingAdminClient
}

export function SchedulingAdminClientProvider({ client, children }: SchedulingAdminClientProviderProps) {
  return <SchedulingAdminClientContext.Provider value={client}>{children}</SchedulingAdminClientContext.Provider>
}

export function useSchedulingAdminClient(): SchedulingAdminClient {
  const client = useContext(SchedulingAdminClientContext)
  if (!client) throw new Error('SchedulingAdminClientProvider is missing')
  return client
}
