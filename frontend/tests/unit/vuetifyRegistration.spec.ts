import { describe, it, expect } from 'vitest'
import { vuetify } from '../../src/vuetify'
import { createApp } from 'vue'

/**
 * `src/vuetify.ts` registers only the components the templates use, so the main chunk does not
 * carry all of Vuetify. The price is that a component used in a template but missing from that
 * list renders as an unknown element - silently, in the browser. This test reads every template
 * and fails instead.
 */
describe('Vuetify registration', () => {
  const sources = import.meta.glob('../../src/**/*.vue', { query: '?raw', import: 'default', eager: true })

  const pascal = (tag: string) =>
    tag.replace(/^v-/, 'V-').replace(/-([a-z])/g, (_m, c: string) => c.toUpperCase()).replace(/-/g, '')

  it('registers every Vuetify component a template uses', () => {
    const app = createApp({})
    app.use(vuetify)
    const registered = new Set(Object.keys(app._context.components))

    const used = new Set<string>()
    for (const source of Object.values(sources) as string[]) {
      for (const match of source.matchAll(/<(v-[a-z-]+)/g)) used.add(pascal(match[1]!))
    }

    expect(used.size).toBeGreaterThan(0)
    expect([...used].filter((name) => !registered.has(name))).toEqual([])
  })
})
