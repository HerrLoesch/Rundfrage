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
    .replace(/<!--[\s\S]*?-->/g, '')                  // comments
    .replace(/\{\{[\s\S]*?\}\}/g, '')                 // interpolations
    // Tags, allowing '>' inside quoted attribute values. A plain /<[^>]+>/ ends the tag at the
    // first '>' it sees - so `v-if="count > 0"` left the rest of the line looking like text and
    // reported a literal that was not there.
    .replace(/<(?:"[^"]*"|'[^']*'|[^'">])*>/g, '\n')
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => /[A-Za-zÄÖÜäöüß]{2,}/.test(line))
}

/**
 * Vuetify takes most user-facing text as a *prop* rather than as template content, so a
 * hard-coded `label="Benutzername"` would slip past a scanner that only reads text nodes. These
 * are the props that end up in front of a person.
 *
 * `alt` belongs here for the same reason and is easier to forget: it is read aloud rather than
 * displayed, so nobody notices it in the browser.
 */
const TEXT_PROPS = ['label', 'placeholder', 'hint', 'title', 'subtitle', 'text', 'aria-label', 'alt']

function literalPropsIn(file: string): string[] {
  const source = readFileSync(file, 'utf8')
  const template = /<template>([\s\S]*?)<\/template>/.exec(source)?.[1]
  if (!template) return []

  const found: string[] = []
  for (const prop of TEXT_PROPS) {
    // Unbound form only: `label="..."`. A bound `:label="t('...')"` is the correct shape and is
    // deliberately not matched.
    const pattern = new RegExp(`(?<![:\\w-])${prop}="([^"]*)"`, 'g')
    for (const match of template.matchAll(pattern)) {
      if (/[A-Za-zÄÖÜäöüß]{2,}/.test(match[1])) found.push(`${prop}="${match[1]}"`)
    }
  }

  return found
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

  it.each(files)('%s passes no literal text as a prop', (file) => {
    expect(literalPropsIn(file)).toEqual([])
  })
})
