import { describe, it, expect } from 'vitest'
import { mountInApp, de } from '../support/mount'

import BareShell from '../../src/components/BareShell.vue'

/**
 * The chrome of the surface that carries no navigation: the participant views and the sign-in
 * form (007 FR-009, FR-008).
 *
 * This used to mount App.vue, which drew the bar itself behind a conditional on the path. App is
 * now nothing but the Vuetify root, and the two surfaces draw their own chrome in layout routes -
 * so the wordmark is asserted where it now lives. The behaviour asserted is unchanged.
 *
 * RouterLink and RouterView are registered by the router plugin, which is not installed here.
 * The link is stubbed as the anchor it renders in the browser, so what the test reads is the
 * destination a person would actually follow.
 */
const shell = () =>
  mountInApp(BareShell, {
    global: {
      stubs: {
        RouterLink: { props: ['to'], template: '<a :href="to"><slot /></a>' },
        RouterView: true,
      },
    },
  })

describe('Participant and sign-in chrome', () => {
  it('carries the wordmark, and it leads home', () => {
    const brand = shell().get('[data-testid="brand"]')

    expect(brand.get('img').attributes('src')).toMatch(/rundfrage-logo\.svg/)
    expect(brand.attributes('href')).toBe('/')
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

  /**
   * The point of splitting the chrome. A participant must not be offered a route into an area
   * they cannot enter, and this surface is the one they land on (Principle I, FR-009).
   */
  it('offers no navigation and no route into the admin area', () => {
    const wrapper = shell()

    expect(wrapper.find('[data-testid="admin-nav"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="admin-shell"]').exists()).toBe(false)
    expect(wrapper.find('[data-testid="sign-out"]').exists()).toBe(false)
    expect(wrapper.html()).not.toContain('/admin')
  })
})
