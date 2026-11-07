/**
 * The absolute address of a page of this app, for a link that is handed to someone else and
 * therefore has to work outside the current tab.
 */
export function absoluteUrl(path: string): string {
  return `${window.location.origin}${path}`
}

/**
 * A participant or Ersteller address: one route section and the token that is its whole
 * authorisation - `/u/…` for a poll, `/w/…` for a wish list, `/e/…` for an Ersteller and `/f/…`
 * for a form. The token is minted URL-safe, so it is placed in the path as it is.
 */
export function shareUrl(section: 'u' | 'w' | 'e' | 'f', token: string): string {
  return absoluteUrl(`/${section}/${token}`)
}
