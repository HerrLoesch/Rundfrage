import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  createPollAsCreator,
  createWishListAsCreator,
  deletePollAsCreator,
  deleteWishListAsCreator,
  fetchCreatorSurface,
  fetchPollResultsAsCreator,
  fetchWishListAsCreator,
  type ApiProblem,
  type CreatorSurface,
  type PollSummary,
  type PollView,
  type WishItemDraft,
  type WishListDetail,
  type WishListSummary,
} from '../api/client'

/** What the one page currently has open. Component state, never an address (FR-028d, FR-028e). */
export type Disclosure =
  | { kind: 'none' }
  | { kind: 'new-poll' }
  | { kind: 'new-wish-list' }
  | { kind: 'poll'; id: string }
  | { kind: 'wish-list'; id: string }

/**
 * The holder's own two lists (009 FR-024 to FR-028g).
 *
 * <b>Separate from `polls.ts` and `wishLists.ts`, deliberately.</b> Those talk to `/admin/**` and
 * belong to the operator; this talks to `/e/{token}`. Sharing a store would be sharing a URL
 * prefix, and the first mistake would be a creator request going to an admin endpoint - which the
 * server would refuse, but only after the interface had already offered it.
 *
 * <b>The open disclosure lives here rather than in the address.</b> Feature 007 gave every admin
 * destination an address because an operator holding a session loses nothing by it; here the
 * address carries the credential, so each additional one is another place the token is written
 * down (spec Q5, research R-9). Opening a poll's answers must not change the address and must not
 * add a history entry.
 */
export const useCreatorStore = defineStore('creator', () => {
  const token = ref('')
  const name = ref('')
  const polls = ref<PollSummary[]>([])
  const wishLists = ref<WishListSummary[]>([])
  const loading = ref(false)

  const open = ref<Disclosure>({ kind: 'none' })
  const pollView = ref<PollView | null>(null)
  const wishList = ref<WishListDetail | null>(null)

  const loadProblem = ref<ApiProblem | null>(null)
  const problem = ref<ApiProblem | null>(null)

  function apply(surface: CreatorSurface): void {
    name.value = surface.name
    polls.value = surface.polls
    wishLists.value = surface.wishLists
  }

  async function load(linkToken: string): Promise<void> {
    token.value = linkToken
    loading.value = true
    loadProblem.value = null
    try {
      apply(await fetchCreatorSurface(linkToken))
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  /** Reload returns to the top with both lists shown and any unconfirmed form gone (FR-028f). */
  async function reload(): Promise<void> {
    close()
    await load(token.value)
  }

  function close(): void {
    open.value = { kind: 'none' }
    pollView.value = null
    wishList.value = null
    problem.value = null
  }

  function reveal(disclosure: Disclosure): void {
    problem.value = null
    open.value = disclosure
  }

  async function openPoll(pollId: string): Promise<void> {
    problem.value = null
    try {
      pollView.value = await fetchPollResultsAsCreator(token.value, pollId)
      open.value = { kind: 'poll', id: pollId }
    } catch (failure) {
      problem.value = failure as ApiProblem
    }
  }

  async function openWishList(wishListId: string): Promise<void> {
    problem.value = null
    try {
      wishList.value = await fetchWishListAsCreator(token.value, wishListId)
      open.value = { kind: 'wish-list', id: wishListId }
    } catch (failure) {
      problem.value = failure as ApiProblem
    }
  }

  async function createPoll(
    title: string,
    message: string | null,
    days: string[],
  ): Promise<boolean> {
    problem.value = null
    try {
      await createPollAsCreator(token.value, title, message, days)
      await reload()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function createWishList(
    title: string,
    description: string | null,
    targetDate: string,
    items: WishItemDraft[],
  ): Promise<boolean> {
    problem.value = null
    try {
      await createWishListAsCreator(token.value, title, description, targetDate, items)
      await reload()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function removePoll(pollId: string): Promise<boolean> {
    problem.value = null
    try {
      await deletePollAsCreator(token.value, pollId)
      await reload()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function removeWishList(wishListId: string): Promise<boolean> {
    problem.value = null
    try {
      await deleteWishListAsCreator(token.value, wishListId)
      await reload()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  return {
    token,
    name,
    polls,
    wishLists,
    loading,
    open,
    pollView,
    wishList,
    loadProblem,
    problem,
    load,
    reload,
    close,
    reveal,
    openPoll,
    openWishList,
    createPoll,
    createWishList,
    removePoll,
    removeWishList,
  }
})
