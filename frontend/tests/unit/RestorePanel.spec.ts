import { describe, it, expect, vi, beforeEach } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { mountComponent, de } from '../support/mount'
import RestorePanel from '../../src/components/admin/RestorePanel.vue'
import { useMaintenanceStore } from '../../src/stores/maintenance'
import { previewRestore, restoreBackup } from '../../src/api/client'

/**
 * The destructive half of the feature. Driven through the real flow - choose a file, ask for the
 * preview, confirm - with only the API mocked.
 *
 * An earlier version of this suite reached into the component and assigned its internal `preview`
 * ref, which needed a `defineExpose` that existed for no other reason. That is a test shaping the
 * production interface, and it proved only that the template renders a value someone set: it would
 * have passed with the preview request removed entirely.
 */
vi.mock('../../src/api/client', () => ({
  previewRestore: vi.fn(),
  restoreBackup: vi.fn(),
}))

const preview = vi.mocked(previewRestore)
const restore = vi.mocked(restoreBackup)

const mountWith = (enabled: boolean) =>
  mountComponent(RestorePanel, {}, () => {
    useMaintenanceStore().enabled = enabled
  })

/** Selects a file and asks for the preview, the way an operator does. */
async function askForPreview(wrapper: ReturnType<typeof mountWith>) {
  const input = wrapper.findComponent({ name: 'VFileInput' })
  input.vm.$emit('update:modelValue', new File(['backup'], 'rundfrage.db'))
  await wrapper.vm.$nextTick()

  await wrapper.get('[data-testid="restore-check"]').trigger('click')
  await flushPromises()
}

describe('RestorePanel (005 FR-016a, FR-018, FR-024, SC-005, ui-contract §3b)', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    preview.mockResolvedValue({
      pollsInBackup: 3,
      responsesInBackup: 12,
      pollsLost: 2,
      responsesLost: 5,
      expired: [],
    })
    restore.mockResolvedValue({ polls: 3, responses: 12, expired: [] })
  })

  it('is disabled while maintenance mode is off, and says why', () => {
    const wrapper = mountWith(false)

    expect(wrapper.text()).toContain(de.restore.needsMaintenance)
    expect(wrapper.get('[data-testid="restore-check"]').attributes('disabled')).toBeDefined()
  })

  it('becomes usable once maintenance mode is on', () => {
    expect(mountWith(true).text()).not.toContain(de.restore.needsMaintenance)
  })

  it('says plainly that everything will be replaced', () => {
    // FR-001 and SC-005: the operator must be able to tell this apart from adding one poll.
    expect(mountWith(true).text()).toContain(de.restore.warning)
  })

  it('shows no confirmation before a preview has been asked for', () => {
    // FR-018: the confirmation exists to confirm the loss, so it cannot come first.
    expect(mountWith(true).find('[data-testid="restore-confirm"]').exists()).toBe(false)
  })

  it('asks the server what the backup holds, and never replaces anything on the way', async () => {
    const wrapper = mountWith(true)
    await askForPreview(wrapper)

    expect(preview).toHaveBeenCalledOnce()
    expect(restore).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="restore-preview"]').exists()).toBe(true)
  })

  it('names the loss on the confirming button rather than offering a bare OK', async () => {
    const wrapper = mountWith(true)
    await askForPreview(wrapper)

    const confirm = wrapper.get('[data-testid="restore-confirm"]')

    expect(confirm.text()).toContain('2')
    expect(confirm.text().trim().toLowerCase()).not.toBe('ok')
  })

  it('replaces the data only after the confirmation is pressed', async () => {
    const wrapper = mountWith(true)
    await askForPreview(wrapper)

    expect(restore).not.toHaveBeenCalled()

    await wrapper.get('[data-testid="restore-confirm"]').trigger('click')
    await flushPromises()

    expect(restore).toHaveBeenCalledOnce()
    expect(wrapper.find('[data-testid="restore-done"]').exists()).toBe(true)
  })

  it('cancelling leaves the data alone', async () => {
    const wrapper = mountWith(true)
    await askForPreview(wrapper)

    await wrapper.get('[data-testid="restore-cancel"]').trigger('click')

    expect(restore).not.toHaveBeenCalled()
    expect(wrapper.find('[data-testid="restore-confirm"]').exists()).toBe(false)
  })

  it('names polls that came back already past their retention date', async () => {
    // FR-016a. The preview is gone by the time the restore has finished, so the summary has to
    // say it too - otherwise those polls vanish an hour later with no warning ever given.
    restore.mockResolvedValue({ polls: 3, responses: 12, expired: ['Altes Grillfest'] })

    const wrapper = mountWith(true)
    await askForPreview(wrapper)
    await wrapper.get('[data-testid="restore-confirm"]').trigger('click')
    await flushPromises()

    const expired = wrapper.get('[data-testid="restore-done-expired"]')
    expect(expired.text()).toContain('Altes Grillfest')
  })

  it('reports a refusal instead of pretending it worked', async () => {
    preview.mockRejectedValue({ code: 'not_a_backup' })

    const wrapper = mountWith(true)
    await askForPreview(wrapper)

    expect(wrapper.get('[data-testid="restore-error"]').text()).toBe(de.error.not_a_backup)
    expect(wrapper.find('[data-testid="restore-preview"]').exists()).toBe(false)
  })
})
