import { createContext, useContext } from 'react'
import type { PropsWithChildren } from 'react'
import type { AuthorizationAdminClient } from './client'

const AuthorizationAdminClientContext = createContext<AuthorizationAdminClient | null>(null)

export function AuthorizationAdminClientProvider({ client, children }: PropsWithChildren<{ readonly client: AuthorizationAdminClient }>) {
  return <AuthorizationAdminClientContext.Provider value={client}>{children}</AuthorizationAdminClientContext.Provider>
}

export function useAuthorizationAdminClient(): AuthorizationAdminClient {
  const client = useContext(AuthorizationAdminClientContext)
  if (!client) throw new Error('AuthorizationAdminClientProvider is missing.')
  return client
}
