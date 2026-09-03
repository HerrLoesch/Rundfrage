import { test, expect } from '@playwright/test'
import { execSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import path from 'node:path'

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..')

/**
 * Guards the container, which is the only place the defect lives (research.md R-6).
 *
 * The Alpine runtime image ships no zone data. The SDK image does, so neither the unit nor the
 * integration suite can see the problem - they build and run against the SDK. Only an assertion
 * against the actual running image catches it, and only before a poll is ever created.
 */
test.describe('Container time zone (FR-011a, FR-011b)', () => {
  test('the running application image carries zone data for Europe/Berlin', () => {
    const output = execSync(
      'docker compose exec -T app sh -c "cat /usr/share/zoneinfo/Europe/Berlin > /dev/null && echo present || echo missing"',
      { cwd: repoRoot, encoding: 'utf8' },
    ).trim()

    expect(
      output,
      'The runtime image lost its zone data. Every poll creation will throw '
        + 'TimeZoneNotFoundException. Restore `apk add --no-cache tzdata` in docker/Dockerfile.',
    ).toBe('present')
  })
})
