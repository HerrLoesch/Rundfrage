/**
 * API client. Every path is relative, so requests go to the same origin that served the
 * page - in production because one container serves both, in development because the Vite
 * dev server proxies /api (FR-003a, FR-003b). No CORS configuration exists anywhere.
 */

const BASE = '/api/v1'

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

// --- Participant surface (US2 to US5) ----------------------------------------------------
// Every call here is anonymous by design: no session, no header, no account (Principle I).

export type Availability = 'yes' | 'maybe' | 'no'

export interface DayView {
  id: string
  date: string
}

export interface DayTotals {
  dayId: string
  yes: number
  maybe: number
  no: number
}

export interface AnswerView {
  dayId: string
  availability: Availability
}

export interface ResponseRow {
  id: string
  displayName: string
  answers: AnswerView[]
}

export interface PollView {
  title: string
  message: string | null
  days: DayView[]
  totals: DayTotals[]
  responses: ResponseRow[]
  page: number
  pageCount: number
  responseCount: number
}

export interface SubmissionAccepted {
  responseId: string
  editToken: string
}

export interface OwnResponse {
  responseId: string
  displayName: string
  answers: AnswerView[]
  poll: PollView
}

export async function fetchPoll(pollToken: string, page = 1): Promise<PollView> {
  return request<PollView>(`/polls/${encodeURIComponent(pollToken)}?page=${page}`)
}

export async function submitResponse(
  pollToken: string,
  displayName: string,
  answers: AnswerView[],
): Promise<SubmissionAccepted> {
  return request<SubmissionAccepted>(`/polls/${encodeURIComponent(pollToken)}/responses`, {
    method: 'POST',
    body: JSON.stringify({ displayName, answers }),
  })
}

export async function fetchOwnResponse(editToken: string): Promise<OwnResponse> {
  return request<OwnResponse>(`/responses/${encodeURIComponent(editToken)}`)
}

export async function reviseResponse(
  editToken: string,
  displayName: string,
  answers: AnswerView[],
): Promise<OwnResponse> {
  return request<OwnResponse>(`/responses/${encodeURIComponent(editToken)}`, {
    method: 'PUT',
    body: JSON.stringify({ displayName, answers }),
  })
}

export async function fetchPollResults(pollId: string, page = 1): Promise<PollView> {
  return request<PollView>(`/admin/polls/${pollId}?page=${page}`)
}

export async function deletePoll(pollId: string): Promise<void> {
  await request<void>(`/admin/polls/${pollId}`, { method: 'DELETE' })
}

export async function deleteResponse(pollId: string, responseId: string): Promise<void> {
  await request<void>(`/admin/polls/${pollId}/responses/${responseId}`, { method: 'DELETE' })
}

// --- Downloads (003 FR-003, FR-013) -------------------------------------------------------
// Addresses rather than functions returning data. A download is a navigation: the browser sends
// the session cookie, reads Content-Disposition and writes the file where the person keeps their
// files. Fetching the bytes into memory and re-offering them through a blob would mean holding a
// whole poll - or the whole storage - in the tab, and renaming the file ourselves.

/** The consistent copy of the storage (FR-003). */
export const backupUrl = `${BASE}/admin/backup`

/** One poll as JSON (FR-013). */
export function exportUrl(pollId: string): string {
  return `${BASE}/admin/polls/${encodeURIComponent(pollId)}/export`
}
