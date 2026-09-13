import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

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
