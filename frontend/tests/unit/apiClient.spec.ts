import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import {
  backupUrl,
  createPoll,
  exportUrl,
  listPolls,
  signIn,
  signOut,
} from '../../src/api/client'

/**
 * The API client itself, exercised against a stubbed `fetch` rather than being mocked away.
 *
 * Every component test mocks this module, so without these tests the client is the one piece of
 * frontend code nothing checks - and that is exactly where a defect hid: two different error
 * paths existed, one throwing an Error and one throwing the contract's problem shape, so
 * `problem.code` was undefined for half the calls and no caller could react to a 401.
 */
describe('api client', () => {
  const originalFetch = globalThis.fetch

  beforeEach(() => {
    globalThis.fetch = vi.fn()
  })

  afterEach(() => {
    globalThis.fetch = originalFetch
  })

  function respond(status: number, body: unknown, ok = status < 400) {
    vi.mocked(globalThis.fetch).mockResolvedValue({
      ok,
      status,
      json: async () => body,
    } as Response)
  }

  function requestedUrl(): string {
    return vi.mocked(globalThis.fetch).mock.calls[0][0] as string
  }

  it('targets the versioned prefix on the same origin', async () => {
    // FR-003a and FR-006a: relative paths only, so the browser never leaves the origin.
    respond(200, [])
    await listPolls()

    expect(requestedUrl()).toBe('/api/v1/admin/polls')
    expect(requestedUrl().startsWith('http')).toBe(false)
  })

  it.each([
    ['the backup', backupUrl],
    ['an export', exportUrl('p1')],
  ])('addresses %s on the same origin too', (_name, url) => {
    // 003 FR-003, FR-013. These are navigations rather than fetches, so no stubbed fetch can
    // catch a mistake here - an absolute URL would silently send the session cookie somewhere
    // else, or fail because it does not.
    expect(url.startsWith('/api/v1/')).toBe(true)
  })

  it('escapes the poll id in an export address', () => {
    expect(exportUrl('a/b')).toBe('/api/v1/admin/polls/a%2Fb/export')
  })

  it.each([
    ['listPolls', () => listPolls()],
    ['createPoll', () => createPoll('t', null, ['2026-11-20'])],
    ['signIn', () => signIn('u', 'p')],
  ])('%s rejects with the contract problem shape, not a bare Error', async (_name, call) => {
    // This is the defect these tests were written for: a caller that cannot read `code` cannot
    // translate the failure and cannot react to a 401.
    respond(401, { code: 'unauthorized' }, false)

    await expect(call()).rejects.toMatchObject({ code: 'unauthorized' })
  })

  it('passes the limit through so the message can name it', async () => {
    respond(400, { code: 'title_too_long', limit: 300 }, false)

    await expect(createPoll('x'.repeat(301), null, ['2026-11-20'])).rejects.toMatchObject({
      code: 'title_too_long',
      limit: 300,
    })
  })

  it('passes the retry delay through', async () => {
    respond(429, { code: 'account_locked', retryAfterSeconds: 900 }, false)

    await expect(signIn('u', 'p')).rejects.toMatchObject({ retryAfterSeconds: 900 })
  })

  it('falls back to a generic code when the body is not JSON', async () => {
    vi.mocked(globalThis.fetch).mockResolvedValue({
      ok: false,
      status: 500,
      json: async () => {
        throw new SyntaxError('not json')
      },
    } as unknown as Response)

    await expect(listPolls()).rejects.toMatchObject({ code: 'unexpected' })
  })

  it('handles a 204 without trying to parse a body', async () => {
    // signOut answers 204. Parsing an empty body would throw and turn success into failure.
    vi.mocked(globalThis.fetch).mockResolvedValue({
      ok: true,
      status: 204,
      json: async () => {
        throw new SyntaxError('no body')
      },
    } as unknown as Response)

    await expect(signOut()).resolves.toBeUndefined()
  })

  it('sends JSON for state-changing calls', async () => {
    // The JSON content type is the second CSRF barrier: a cross-site HTML form cannot send it.
    respond(201, { id: 'p1' })
    await createPoll('Titel', null, ['2026-11-20'])

    const init = vi.mocked(globalThis.fetch).mock.calls[0][1] as RequestInit
    expect((init.headers as Record<string, string>)['Content-Type']).toBe('application/json')
    expect(init.method).toBe('POST')
  })
})
