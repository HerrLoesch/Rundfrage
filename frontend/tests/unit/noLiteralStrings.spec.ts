import { describe, it, expect } from 'vitest'
import { readFileSync, readdirSync, statSync } from 'node:fs'
import { join } from 'node:path'

/**
 * FR-029 and SC-012: no component may carry a literal user-facing string.
 *
 * This replaces a per-component assertion that claimed to check this and did not - it only
 * confirmed that some expected text was present, which says nothing about whether other text
 * was hard-coded. A requirement about the absence of something has to be checked by looking for
 * it everywhere, not by looking at one component.
 */
function vueFiles(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const path = join(dir, entry)
    if (statSync(path).isDirectory()) return vueFiles(path)
    return path.endsWith('.vue') ? [path] : []
  })
}

function literalsIn(file: string): string[] {
  const source = readFileSync(file, 'utf8')
  const template = /<template>([\s\S]*?)<\/template>/.exec(source)?.[1]
  if (!template) return []

  return template
    .replace(/<!--[\s\S]*?-->/g, '')     // comments
    .replace(/\{\{[\s\S]*?\}\}/g, '')    // interpolations
    .replace(/<[^>]+>/g, '\n')           // tags, including their attributes
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => /[A-Za-zÄÖÜäöüß]{2,}/.test(line))
}

describe('no literal user-facing strings (FR-029)', () => {
  const files = vueFiles(join(import.meta.dirname, '../../src'))

  it('finds components to check', () => {
    // Without this, a broken glob would make the suite pass by examining nothing.
    expect(files.length).toBeGreaterThan(3)
  })

  it.each(files)('%s carries no literal text in its template', (file) => {
    expect(literalsIn(file)).toEqual([])
  })
})
