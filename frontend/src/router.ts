import { createRouter, createWebHistory, type RouteRecordRaw, type Router } from 'vue-router'

/**
 * Two surfaces, split by who may reach them.
 *
 * Everything under the admin shell needs the operator session; everything else must never need
 * one - the capability is the token in the path (Principle I). There is no navigation guard, and
 * the comment at the end of this file explains why the honest refusal is server-side (002 FR-048).
 *
 * The admin areas are *children of the shell* rather than siblings that each draw their own
 * chrome. That is what makes FR-001 a property rather than a coincidence: Vue Router keeps the
 * parent mounted across child navigations, so the navigation bar persists without anyone
 * managing it, and it is also what makes FR-008 structural - the shell wraps exactly the routes
 * that need a session, and the sign-in form sits outside it (research.md R-1).
 *
 * Addresses are German, continuing /admin/anmelden rather than starting a second scheme (R-5).
 */
export const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: { name: 'dashboard' },
  },
  {
    // The surface with no navigation. Its children carry absolute paths, so this record is a
    // layout and nothing else: it contributes chrome, not a path segment.
    //
    // Declared before the admin shell so that /admin/anmelden is matched here rather than by the
    // shell's catch-all. Vue Router would rank the static segment above the catch-all anyway;
    // the order makes it true by reading as well as by scoring.
    path: '/',
    component: () => import('./components/BareShell.vue'),
    children: [
      {
        // Outside the shell on purpose: there is no session yet, so there are no areas to list,
        // and a navigation bar here would offer what the server is about to refuse (FR-008).
        path: '/admin/anmelden',
        name: 'sign-in',
        component: () => import('./components/admin/SignInForm.vue'),
      },
      {
        // The participant capability. No session, no guard, no navigation, nothing in front of
        // it (002 FR-021, 007 FR-009).
        path: '/u/:pollToken',
        name: 'poll',
        component: () => import('./components/poll/PollView.vue'),
        props: true,
      },
      {
        // The per-response capability (002 FR-026, FR-028).
        path: '/a/:editToken',
        name: 'response',
        component: () => import('./components/poll/PollView.vue'),
        props: true,
      },
      {
        // A wish list's participant capability, beside the poll's and with the same properties:
        // no session, no guard, no navigation, nothing in front of it (008 FR-014, FR-026).
        path: '/w/:listToken',
        name: 'wish-list-public',
        component: () => import('./components/wish/WishListView.vue'),
        props: true,
      },
      {
        // The per-submission capability - "z" for Zusage, beside "a" for Antwort
        // (008 FR-022, research R-8).
        path: '/z/:claimToken',
        name: 'claim',
        component: () => import('./components/wish/ClaimView.vue'),
        props: true,
      },
      {
        // An Ersteller's whole surface - "e" for Ersteller, the fifth one-letter capability path
        // beside u, a, w and z (009 FR-006, research R-7).
        //
        // EXACTLY ONE ROUTE, and deliberately childless. Feature 007 gave every admin destination
        // an address of its own because an operator holding a session loses nothing by it; here
        // the address IS the credential, so each additional one is another place the token is
        // written down - in history, in a copied link, in a screenshot. A poll's answers and a
        // wish list's detail therefore open as component state inside this page, the address does
        // not change, and no history entry is added (009 FR-028d, FR-028e, spec Q5, research R-9).
        //
        // A reviewer comparing this with /admin/terminfindungen/:pollId will see the asymmetry and
        // be tempted to fix it. The asymmetry is the decision; the cost is stated in the spec and
        // paid for by FR-028g, which requires the lists to carry enough summary to find things
        // without opening each in turn.
        path: '/e/:creatorToken',
        name: 'creator',
        component: () => import('./components/creator/CreatorSurface.vue'),
        props: true,
      },
      {
        // A form's participant capability - "f" for Formular, the sixth one-letter capability
        // path beside u, a, w, z and e (010 FR-012). No session, no guard, no navigation.
        path: '/f/:formToken',
        name: 'form',
        component: () => import('./components/form/FormFillView.vue'),
        props: true,
      },
    ],
  },
  {
    path: '/admin',
    name: 'admin-shell',
    component: () => import('./components/admin/AdminShell.vue'),
    children: [
      {
        // The index child, which is why "entering the admin area without naming an area" needs
        // no redirect to write and no rule to remember (FR-006). It also keeps the operator's
        // existing /admin bookmark working.
        path: '',
        name: 'dashboard',
        component: () => import('./components/admin/DashboardView.vue'),
      },
      {
        path: 'terminfindungen',
        name: 'polls',
        component: () => import('./components/admin/PollList.vue'),
      },
      {
        // One poll's answers: an address of its own, so the largest screen in the admin area can
        // be linked to and survives a reload like every other (FR-014a, SC-006).
        path: 'terminfindungen/:pollId',
        name: 'poll-answers',
        component: () => import('./components/admin/PollAnswersView.vue'),
        props: true,
      },
      {
        // The second feature area. Feature 007 built the navigation to hold more than one and
        // refused to place a placeholder in it; this is the first entry to take one of those
        // places (008 FR-041).
        path: 'wunschlisten',
        name: 'wish-lists',
        component: () => import('./components/admin/WishListsView.vue'),
      },
      {
        // One wish list: an address of its own, so it can be linked to and survives a reload
        // like every other admin address (008 FR-043, SC-008).
        path: 'wunschlisten/:wishListId',
        name: 'wish-list',
        component: () => import('./components/admin/WishListDetailView.vue'),
        props: true,
      },
      {
        // The third feature area, and the last thing before settings. An Ersteller is a
        // capability of the installation, not a setting of it, which is why it sits here rather
        // than inside Einstellungen (009 FR-043, FR-047).
        //
        // One route, with no detail destination: an Ersteller is a name, a link and two counts,
        // which is a row rather than a page.
        path: 'ersteller',
        name: 'creators',
        component: () => import('./components/admin/CreatorsView.vue'),
      },
      {
        // The fourth feature area (010 FR-043-equivalent). Unlike the Ersteller area, a form has
        // a detail destination of its own, following 007's ordinary addressing rule rather than
        // the creator surface's single-address exception - a form is reached only with an
        // operator session already established, so there is no credential in the address to
        // protect (contrast 009 FR-028d).
        path: 'formulare',
        name: 'forms',
        component: () => import('./components/admin/FormsView.vue'),
      },
      {
        path: 'formulare/:formId',
        name: 'form-builder',
        component: () => import('./components/admin/FormBuilderView.vue'),
        props: true,
      },
      {
        path: 'einstellungen',
        name: 'settings',
        component: () => import('./components/admin/SettingsView.vue'),
      },
      {
        // An address under the admin area that means nothing still means something definite
        // (FR-007). Sent to the dashboard rather than to an empty shell or an error page.
        path: ':unknown(.*)',
        redirect: { name: 'dashboard' },
      },
    ],
  },
]

export const router = createRouter({
  history: createWebHistory(),
  routes,
})

/**
 * A stale shell must not swallow a click.
 *
 * Every area is loaded lazily, by a chunk named after its content. A tab that was opened before an
 * update still runs the old shell, and the old shell asks for chunk names the new build no longer
 * ships. The import then rejects, the navigation is aborted, and without this nothing at all is
 * shown: the address stays where it was and the only trace is a console error. That is the
 * "clicking settings does nothing" bug, and settings was merely the first area nobody had opened
 * before the update.
 *
 * The server asks the browser to revalidate index.html on every load, so a full load of the
 * destination is the recovery: it fetches the current shell and the current chunk with it. Once
 * per destination, remembered across the reload - if the fresh shell fails the same way the
 * problem is not staleness, and reloading forever would be worse than the error. The mark is
 * cleared as soon as the destination is reached, so the next update is recovered from just the
 * same.
 *
 * Installed on any router rather than written against the one below, so the unit tests can drive
 * it with a memory history and a stand-in for the full load.
 */
const failedImport =
  /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module/i

export function recoverFromStaleShell(
  target: Router,
  fullLoad: (path: string) => void = (path) => window.location.assign(path),
): void {
  const mark = (path: string) => `reloaded-for:${path}`

  target.onError((error, to) => {
    if (!(error instanceof Error) || !failedImport.test(error.message)) return
    if (sessionStorage.getItem(mark(to.fullPath))) return

    sessionStorage.setItem(mark(to.fullPath), '1')
    fullLoad(to.fullPath)
  })

  target.afterEach((to) => {
    sessionStorage.removeItem(mark(to.fullPath))
  })
}

recoverFromStaleShell(router)

// There is deliberately no navigation guard.
//
// The session lives in an HttpOnly cookie the client cannot read, so the only honest way to
// know whether it is valid is to ask the server. A guard reading client-side state answered
// "not signed in" after every page reload - the state is rebuilt empty while the cookie is
// still perfectly valid - and bounced the operator back to the form.
//
// The server is the authority (002 FR-048). Each area simply asks, and redirects if refused
// (007 FR-011). Signing in then leads to the dashboard and never back to what was interrupted
// (007 FR-011a).
