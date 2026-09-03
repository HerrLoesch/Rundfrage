import { describe, it, expect, vi, beforeEach } from 'vitest'
import { mountComponent, de } from '../support/mount'

vi.mock('../../src/api/client', () => ({
  signIn: vi.fn(),
  signOut: vi.fn(),
  fetchMessage: vi.fn(),
  fetchDatabaseStatus: vi.fn(),
  listPolls: vi.fn(),
  createPoll: vi.fn(),
}))
vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }))

import { signIn } from '../../src/api/client'
import SignInForm from '../../src/components/admin/SignInForm.vue'

const mountForm = () => mountComponent(SignInForm)

/** Vuetify wraps the control; the native input is what a person actually types into. */
const inputIn = (wrapper: ReturnType<typeof mountForm>, testid: string) =>
  wrapper.get(`[data-testid="${testid}"]`).find('input')

describe('SignInForm (FR-001, FR-004)', () => {
  beforeEach(() => vi.clearAllMocks())

  it('offers a user and a password field and a submit control', () => {
    const wrapper = mountForm()

    expect(inputIn(wrapper, 'sign-in-user').exists()).toBe(true)
    expect(inputIn(wrapper, 'sign-in-password').exists()).toBe(true)
    expect(wrapper.find('[data-testid="sign-in-submit"]').exists()).toBe(true)
  })

  it('masks the password field', () => {
    expect(inputIn(mountForm(), 'sign-in-password').attributes('type')).toBe('password')
  })

  it('labels both fields, so neither is an unlabelled box', () => {
    // FR-051
    const wrapper = mountForm()

    expect(wrapper.text()).toContain(de.signIn.user)
    expect(wrapper.text()).toContain(de.signIn.password)
  })

  it('shows one neutral error that names neither field', async () => {
    // FR-004: the message must not say which half was wrong. Asserted against the translation,
    // so a future rewording that starts naming the field breaks this test.
    vi.mocked(signIn).mockRejectedValue({ code: 'unauthorized' })

    const wrapper = mountForm()
    await wrapper.get('[data-testid="sign-in-form"]').trigger('submit')
    await new Promise((r) => setTimeout(r, 0))

    const error = wrapper.get('[data-testid="sign-in-error"]').text()
    expect(error).toContain(de.signIn.failed)
    expect(error.toLowerCase()).not.toContain('benutzer')
    expect(error.toLowerCase()).not.toContain('passwort')
  })

  it('shows the lockout message with the retry delay', async () => {
    // FR-005: the refusal says when to try again.
    vi.mocked(signIn).mockRejectedValue({ code: 'account_locked', retryAfterSeconds: 900 })

    const wrapper = mountForm()
    await wrapper.get('[data-testid="sign-in-form"]').trigger('submit')
    await new Promise((r) => setTimeout(r, 0))

    expect(wrapper.get('[data-testid="sign-in-error"]').text()).toContain('15')
  })

  it('renders its heading from the translation file', () => {
    expect(mountForm().text()).toContain(de.signIn.title)
  })
})
