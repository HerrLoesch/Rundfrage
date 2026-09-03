import { test, expect } from '@playwright/test'

/**
 * US1 - the walking skeleton, exercised against the real container set started by
 * `docker compose up` (FR-021). Proves browser -> application -> backend.
 */
test.describe('Walking skeleton', () => {
  test('the page displays text retrieved from the backend', async ({ page }) => {
    await page.goto('/status')

    const message = page.getByTestId('backend-message')
    await expect(message).toBeVisible()
    await expect(message).not.toBeEmpty()
  })

  test('the backend endpoint answers directly with the same text', async ({ page, request }) => {
    // FR-006 / FR-007: the page must show what the endpoint returns, not its own literal.
    const response = await request.get('/api/v1/message')
    expect(response.status()).toBe(200)

    const { message } = await response.json()
    expect(message).toBeTruthy()

    await page.goto('/status')
    await expect(page.getByTestId('backend-message')).toContainText(message)
  })

  test('the application is served from a single origin', async ({ page }) => {
    // FR-003a: no cross-origin request may be needed to reach the API.
    // The expected origin is fixed up front. Deriving it from page.url() inside the listener
    // does not work: when the first request fires the page is still about:blank, whose origin
    // is "null", so every request would look cross-origin.
    const expectedOrigin = new URL(test.info().project.use.baseURL ?? 'http://localhost:8080')
      .origin

    const crossOrigin: string[] = []
    page.on('request', (r) => {
      if (new URL(r.url()).origin !== expectedOrigin) crossOrigin.push(r.url())
    })

    await page.goto('/status')
    await page.waitForLoadState('networkidle')

    // No CDN, no external fonts, no analytics - constitution Principle IV requires every
    // asset to come from the application's own origin.
    expect(crossOrigin).toEqual([])
  })
})
