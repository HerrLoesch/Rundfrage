import { describe, it, expect } from 'vitest'
import { mountComponent, de } from '../support/mount'
import ShareLink from '../../src/components/poll/ShareLink.vue'

/**
 * One component renders two of the three addresses in the system: the creator's, shown right
 * after a poll is made, and the participant's own, shown right after an answer is submitted.
 *
 * That is why the clarification came out as "all three" rather than "the admin area only" — the
 * alternative was to make one caller behave differently from the other for no reason a reader
 * could ever discover (004 FR-015a).
 */
const mountShare = (path = '/u/abcdefghijklmnopqrstuv') =>
  mountComponent(ShareLink, { props: { path, label: 'Link zum Teilen' } })

describe('ShareLink (004 FR-015 to FR-017)', () => {
  it('renders the address as a link that opens in a new tab', () => {
    const link = mountShare().get('[data-testid="share-url"]')

    expect(link.element.tagName).toBe('A')
    expect(link.attributes('href')).toBe(`${window.location.origin}/u/abcdefghijklmnopqrstuv`)
    expect(link.attributes('target')).toBe('_blank')
  })

  it('denies the opened page any handle on the tab that opened it', () => {
    // FR-016b
    const rel = mountShare().get('[data-testid="share-url"]').attributes('rel') ?? ''

    expect(rel).toContain('noopener')
    expect(rel).toContain('noreferrer')
  })

  it('says a new tab will open, without saying it on screen or inside the link', () => {
    // FR-016a. The note describes the link; it is not part of it. Written the other way round
    // first, and eleven end-to-end tests said so: the address they read had the note glued to
    // its end, so every navigation built from it went nowhere.
    const wrapper = mountShare()
    const link = wrapper.get('[data-testid="share-url"]')

    expect(link.find('.d-sr-only').exists()).toBe(false)

    const note = wrapper.get(`#${link.attributes('aria-describedby')}`)
    expect(note.text()).toBe(de.share.newTab)
    expect(note.classes()).toContain('d-sr-only')
  })

  it('leaves the link text as the bare address', () => {
    // FR-017. A plain text selection and the copy control must both yield the address and
    // nothing else.
    const link = mountShare().get('[data-testid="share-url"]')

    expect(link.text()).toBe(`${window.location.origin}/u/abcdefghijklmnopqrstuv`)
  })

  it('honours the identifier its caller asks for', () => {
    // The creator's confirmation labels its address differently from the participant's, and the
    // end-to-end suite addresses them by those names.
    const wrapper = mountComponent(ShareLink, {
      props: { path: '/a/token', label: 'Dein persoenlicher Link', linkTestid: 'poll-share-link' },
    })

    expect(wrapper.get('[data-testid="poll-share-link"]').element.tagName).toBe('A')
  })

  it('still offers the copy control beside the link', () => {
    expect(mountShare().find('[data-testid="share-copy"]').exists()).toBe(true)
  })
})
