/**
 * API client. Every path is relative, so requests go to the same origin that served the
 * page - in production because one container serves both, in development because the Vite
 * dev server proxies /api (FR-003a, FR-003b). No CORS configuration exists anywhere.
 */

const BASE = '/api/v1'

/** Matches DatabaseState in contracts/openapi.yaml. The backend has exactly two states. */
export type DatabaseState = 'reachable' | 'unreachable'

/** Matches DatabaseStatusResponse in contracts/openapi.yaml. */
export interface DatabaseStatus {
  state: DatabaseState
  checkedAt: string
  durationMs: number
}

/**
 * Throws on any transport failure or non-2xx response. Callers translate that into the
 * client-side `backendUnreachable` state (research.md R-4).
 */
async function getJson<T>(path: string): Promise<T> {
  const response = await fetch(`${BASE}${path}`)
  if (!response.ok) {
    throw new Error(`Request to ${path} failed with status ${response.status}`)
  }
  return (await response.json()) as T
}

export async function fetchMessage(): Promise<string> {
  const payload = await getJson<{ message: string }>('/message')
  return payload.message
}

export async function fetchDatabaseStatus(): Promise<DatabaseStatus> {
  return getJson<DatabaseStatus>('/status/database')
}
