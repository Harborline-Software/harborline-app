import type { IncomingMessage, ServerResponse } from 'node:http'

export function createSelectedSessionProxy(nodeOrigin: string): (
  request: IncomingMessage, response: ServerResponse, next: () => void,
) => Promise<unknown>
