import { describe, it, expect, vi } from 'vitest'
import { mountComponent, de } from '../support/mount'

vi.mock('vue-router', () => ({ useRoute: () => ({ path: '/admin' }) }))

import App from '../../src/App.vue'

/**
 * RouterLink and RouterView are registered by the router plugin, which is not installed here.
 * The link is stubbed as the anchor it renders in the browser, so what the test reads is the
 * destination a person would actually follow.
 */
const shell = () =>
  mountComponent(App, {
    global: {
      stubs: {
        RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
        RouterView: true,
      },
    },
  })

describe('Application chrome', () => {
  it('carries the wordmark, and it leads home', () => {
    const brand = shell().get('[data-testid="brand"]')

    expect(brand.get('img').attributes('src')).toMatch(/rundfrage-logo\.svg/)
    expect(brand.attributes('href')).toBe('/admin')
  })

  it('gives the wordmark an accessible name from the translations', () => {
    // The image is the link's only content. Without a name the link is announced as "graphic"
    // and leads nowhere a screen reader can describe - and a German literal here would be the
    // one piece of user-facing text the literal scanner never looked at.
    expect(shell().get('[data-testid="brand"] img').attributes('alt')).toBe(de.app.title)
  })

  it('states the size of the wordmark so the bar does not jump while it loads', () => {
    const logo = shell().get('[data-testid="brand"] img')

    expect(logo.attributes('width')).toBeTruthy()
    expect(logo.attributes('height')).toBeTruthy()
  })
})
