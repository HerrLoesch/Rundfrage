import { describe, it, expect, vi, beforeEach } from 'vitest'
import { defineComponent } from 'vue'
import { createRouter, createMemoryHistory, type RouteComponent } from 'vue-router'
import { recoverFromStaleShell } from '../../src/router'

/**
 * The tab that outlived an update.
 *
 * Its shell asks for a chunk the new build no longer ships, so the lazy import of the area rejects.
 * Before, that left the navigation aborted and the screen unchanged; now it must lead to one full
 * load of the destination - and only one, so a server that genuinely cannot serve the area does
 * not send the browser round in circles.
 */
const Area = defineComponent({ template: '<div />' })

const staleImport = () =>
  Promise.reject(
    new TypeError('Failed to fetch dynamically imported module: /assets/SettingsView-CRmhPUMb.js'),
  )

function routerWith(settings: RouteComponent | (() => Promise<RouteComponent>)) {
  const fullLoad = vi.fn<(path: string) => void>()
  const r = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/admin', name: 'dashboard', component: Area },
      { path: '/admin/einstellungen', name: 'settings', component: settings },
    ],
  })
  recoverFromStaleShell(r, fullLoad)
  return { r, fullLoad }
}

describe('Recovering from a stale shell', () => {
  beforeEach(() => sessionStorage.clear())

  it('loads the destination in full when its chunk cannot be fetched', async () => {
    const { r, fullLoad } = routerWith(staleImport)
    await r.push('/admin')

    await r.push('/admin/einstellungen').catch(() => undefined)

    expect(fullLoad).toHaveBeenCalledWith('/admin/einstellungen')
  })

  it('does so once per destination, so a genuine failure does not loop', async () => {
    const { r, fullLoad } = routerWith(staleImport)
    await r.push('/admin')

    await r.push('/admin/einstellungen').catch(() => undefined)
    await r.push('/admin/einstellungen').catch(() => undefined)

    expect(fullLoad).toHaveBeenCalledTimes(1)
  })

  it('forgets the attempt once the destination is reached, ready for the next update', async () => {
    const { r, fullLoad } = routerWith(staleImport)
    await r.push('/admin')
    await r.push('/admin/einstellungen').catch(() => undefined)
    expect(fullLoad).toHaveBeenCalledTimes(1)

    // The full load brought the fresh shell, in which the area loads. Modelled as a second router
    // sharing the same sessionStorage, which is what a reload amounts to.
    const fresh = routerWith(Area)
    await fresh.r.push('/admin/einstellungen')
    expect(fresh.fullLoad).not.toHaveBeenCalled()

    // And when the *next* build makes this shell stale in turn, it is recovered from again.
    const staleAgain = routerWith(staleImport)
    await staleAgain.r.push('/admin')
    await staleAgain.r.push('/admin/einstellungen').catch(() => undefined)
    expect(staleAgain.fullLoad).toHaveBeenCalledTimes(1)
  })

  it('leaves every other navigation error alone', async () => {
    const { r, fullLoad } = routerWith(() => Promise.reject(new Error('something unrelated')))
    await r.push('/admin')

    await r.push('/admin/einstellungen').catch(() => undefined)

    expect(fullLoad).not.toHaveBeenCalled()
  })
})
