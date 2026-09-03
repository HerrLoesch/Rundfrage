import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import de from '../../src/locales/de.json'

vi.mock('../../src/api/client', () => ({
  signIn: vi.fn(),
  signOut: vi.fn(),
  fetchMessage: vi.fn(),
  fetchDatabaseStatus: vi.fn(),
  listPolls: vi.fn(),
  createPoll: vi.fn(),
}))

import { signIn } from '../../src/api/client'
import SignInForm from '../../src/components/admin/SignInForm.vue'

const i18n = createI18n({ legacy: false, locale: 'de', messages: { de } })
const mountForm = () => mount(SignInForm, { global: { plugins: [i18n] } })

describe('SignInForm (FR-001, FR-004)', () => {
  beforeEach(() => setActivePinia(createPinia()))

  it('offers a user and a password field and a submit control', () => {
    const wrapper = mountForm()

    expect(wrapper.find('[data-testid="sign-in-user"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sign-in-password"]').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sign-in-submit"]').exists()).toBe(true)
  })

  it('masks the password field', () => {
    const wrapper = mountForm()

    expect(wrapper.get('[data-testid="sign-in-password"]').attributes('type')).toBe('password')
  })

  it('shows one neutral error that names neither field', async () => {
    // FR-004: the message must not say which half was wrong. Asserted against the translation,
    // so a future rewording that starts naming the field breaks this test.
    vi.mocked(signIn).mockRejectedValue({ code: 'unauthorized' })

    const wrapper = mountForm()
    await wrapper.get('[data-testid="sign-in-submit"]').trigger('submit')
    await new Promise((r) => setTimeout(r, 0))

    const error = wrapper.get('[data-testid="sign-in-error"]').text()
    expect(error).toBe(de.signIn.failed)
    expect(error.toLowerCase()).not.toContain('benutzer')
    expect(error.toLowerCase()).not.toContain('passwort')
  })

  it('shows the lockout message with the retry delay', async () => {
    // FR-005: the refusal says when to try again.
    vi.mocked(signIn).mockRejectedValue({ code: 'account_locked', retryAfterSeconds: 900 })

    const wrapper = mountForm()
    await wrapper.get('[data-testid="sign-in-submit"]').trigger('submit')
    await new Promise((r) => setTimeout(r, 0))

    expect(wrapper.get('[data-testid="sign-in-error"]').text()).toContain('15')
  })

  it('renders its labels from the translation file', () => {
    const wrapper = mountForm()

    expect(wrapper.text()).toContain(de.signIn.title)
    expect(wrapper.text()).toContain(de.signIn.user)
    expect(wrapper.text()).toContain(de.signIn.password)
  })
})
