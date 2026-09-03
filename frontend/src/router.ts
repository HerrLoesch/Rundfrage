import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

/**
 * Three destinations behind four routes, plus a redirect, split by who may reach them.
 *
 * Everything under /admin needs the operator session; everything else must never need one -
 * the capability is the token in the path (Principle I). There is no navigation guard, and the
 * comment at the end of this file explains why the honest refusal is server-side (FR-048).
 */
const routes: RouteRecordRaw[] = [
  {
    path: '/',
    redirect: { name: 'admin-polls' },
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
  {
    // The participant capability. No session, no guard, nothing in front of it (FR-021).
    path: '/u/:pollToken',
    name: 'poll',
    component: () => import('./components/poll/PollView.vue'),
    props: true,
  },
  {
    // The per-response capability (FR-026, FR-028).
    path: '/a/:editToken',
    name: 'response',
    component: () => import('./components/poll/PollView.vue'),
    props: true,
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
// The server is the authority (FR-048). The admin view simply asks, and redirects if refused.
