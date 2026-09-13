import { mount, type ComponentMountingOptions } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'
import de from '../../src/locales/de.json'

/**
 * One place that mounts a component the way the application does.
 *
 * Components render through Vuetify now, so a test that mounts without it exercises a different
 * component tree than production - which is exactly how a passing test can end up describing
 * something that does not work.
 */
export const i18n = createI18n({
  legacy: false,
  locale: 'de',
  messages: { de },
  datetimeFormats: {
    de: {
      short: { day: '2-digit', month: '2-digit' },
      long: { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' },
    },
  },
})

export const vuetify = createVuetify({ components, directives })

/**
 * @param prepare runs after the store is activated but *before* the component renders. State
 * set after mounting is not visible to the first render, which quietly turns "no days shown"
 * into "the test asserted against an empty component".
 */
export function mountComponent<T>(
  component: T,
  options: ComponentMountingOptions<T> = {},
  prepare?: () => void,
) {
  setActivePinia(createPinia())
  prepare?.()

  // Plugins are *merged*, not replaced. Spreading options.global over a `plugins` key would
  // silently drop i18n and Vuetify the moment a caller supplied a router, and the component
  // would then fail to resolve every Vuetify element - a failure that reads like a broken
  // component rather than a broken mount.
  const { plugins = [], ...restGlobal } = options.global ?? {}

  return mount(component, {
    ...options,
    global: { ...restGlobal, plugins: [i18n, vuetify, ...plugins] },
  })
}

/**
 * Mounts a component inside a `v-app`.
 *
 * Vuetify's layout components - v-app-bar, v-navigation-drawer, v-main - register themselves with
 * a layout their parent provides, and throw "Could not find injected layout" without one. App.vue
 * used to supply it, so specs that mounted App got it for free; now that each surface draws its
 * own chrome in a layout route, the specs for those need the root themselves.
 */
export function mountInApp<T>(
  component: T,
  options: ComponentMountingOptions<T> = {},
  prepare?: () => void,
) {
  const host = {
    components: { Subject: component as object },
    template: '<v-app><Subject /></v-app>',
  }

  return mountComponent(host as never, options as never, prepare)
}

export { de }
