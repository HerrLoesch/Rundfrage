import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

/**
 * Four destinations, split by who may reach them.
 *
 * Everything under /admin needs the operator session. Everything else must never need one -
 * the capability is the token in the path (Principle I). The guard below only redirects; the
 * real refusal is server-side (FR-048), because a client-side guard protects nothing.
 */
const routes: RouteRecordRaw[] = [
  {
    // The application, not a diagnostic. Until the participant views arrive with US2 the admin
    // area is the whole of it, so the root leads there.
    path: '/',
    redirect: { name: 'admin-polls' },
  },
  {
    // Feature 001's walking-skeleton page. It still shows the backend text and the database
    // state that FR-007 and FR-010 of that feature require - those requirements say the web
    // application must display them, not that they must occupy the front door.
    path: '/status',
    name: 'status',
    component: () => import('./components/SystemStatus.vue'),
  },
  {
    path: '/admin/anmelden',
    name: 'sign-in',
    component: () => import('./components/admin/SignInForm.vue'),
  },
  {
    path: '/admin',
    name: 'admin-polls',
    component: () => import('./components/admin/PollList.vue'),
  },
  // The participant routes - /u/:pollToken and /a/:editToken - are registered by US2 and US4,
  // together with the components they load. Declaring them ahead of their components does not
  // merely fail at runtime: Vite resolves dynamic imports at build time, so an unwritten
  // component breaks the production build outright.
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
// The server is the authority (FR-048). The admin view simply asks, and redirects if refused.
