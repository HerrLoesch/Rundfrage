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

/** A machine-readable failure from the API. The frontend owns the words (FR-029). */
export interface ApiProblem {
  code: string
  limit?: number
  retryAfterSeconds?: number
}

/**
 * The single request path. Every call rejects with {@link ApiProblem} - never with a bare
 * Error - so a caller can always read `code`, translate it, and react to a 401.
 *
 * There were briefly two paths here: this one and a second that threw `new Error(...)`. Half
 * the calls therefore produced a rejection with no `code`, the admin view could not tell an
 * expired session from any other failure, and the operator was left staring at a page that
 * silently failed to load. One path, always the same shape.
 */
async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await fetch(`${BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...(init.headers ?? {}) },
  })

  if (!response.ok) {
    let problem: ApiProblem = { code: 'unexpected' }
    try {
      problem = (await response.json()) as ApiProblem
    } catch {
      // A response without a JSON body stays "unexpected" rather than becoming a parse error.
    }
    throw problem
  }

  // 204 carries no body; parsing one would turn a success into a failure.
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

async function getJson<T>(path: string): Promise<T> {
  return request<T>(path)
}

export async function fetchMessage(): Promise<string> {
  const payload = await getJson<{ message: string }>('/message')
  return payload.message
}

export async function fetchDatabaseStatus(): Promise<DatabaseStatus> {
  return getJson<DatabaseStatus>('/status/database')
}

// ---------------------------------------------------------------------------------------
// Feature 002
// ---------------------------------------------------------------------------------------

export interface PollSummary {
  id: string
  title: string
  participantToken: string
  retentionDeadline: string
  responseCount: number
  dayCount: number
}

export async function signIn(user: string, password: string): Promise<void> {
  await request<void>('/admin/session', { method: 'POST', body: JSON.stringify({ user, password }) })
}

export async function signOut(): Promise<void> {
  await request<void>('/admin/session', { method: 'DELETE' })
}

export async function listPolls(): Promise<PollSummary[]> {
  return getJson<PollSummary[]>('/admin/polls')
}

export async function createPoll(
  title: string,
  message: string | null,
  days: string[],
): Promise<PollSummary> {
  return request<PollSummary>('/admin/polls', {
    method: 'POST',
    body: JSON.stringify({ title, message, days }),
  })
}
