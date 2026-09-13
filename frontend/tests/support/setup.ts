// jsdom implements neither of these, and Vuetify's layout components use both.
globalThis.ResizeObserver ??= class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as never

globalThis.matchMedia ??= ((query: string) => ({
  matches: false,
  media: query,
  onchange: null,
  addListener: () => {},
  removeListener: () => {},
  addEventListener: () => {},
  removeEventListener: () => {},
  dispatchEvent: () => false,
})) as never

// Vuetify's overlay positioning reads visualViewport, which jsdom does not implement either.
// Without it every v-dialog throws on mount - including the delete confirmations, which are the
// controls whose *asking first* is the behaviour worth testing (008 FR-034, FR-038).
globalThis.visualViewport ??= {
  width: 1024,
  height: 768,
  offsetLeft: 0,
  offsetTop: 0,
  pageLeft: 0,
  pageTop: 0,
  scale: 1,
  addEventListener: () => {},
  removeEventListener: () => {},
  dispatchEvent: () => false,
  onresize: null,
  onscroll: null,
} as never
