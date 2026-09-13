import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  addWishItem,
  createWishList as apiCreate,
  deleteWishClaim,
  deleteWishList as apiDelete,
  fetchWishList,
  listWishLists,
  removeWishItem,
  updateWishItem,
  updateWishList,
  type ApiProblem,
  type WishItemDraft,
  type WishListDetail,
  type WishListSummary,
} from '../api/client'

/**
 * The operator's side of wish lists.
 *
 * Two problem fields rather than one, for the reason the poll store records: reading the list and
 * writing a list fail for unrelated reasons, and the interface says different things about them.
 * A rejected item name belongs in the form; an unreachable store belongs above the list.
 */
export const useWishListsStore = defineStore('wishLists', () => {
  const lists = ref<WishListSummary[]>([])
  const current = ref<WishListDetail | null>(null)
  const loading = ref(false)

  const loadProblem = ref<ApiProblem | null>(null)
  const problem = ref<ApiProblem | null>(null)

  async function load(): Promise<void> {
    loading.value = true
    loadProblem.value = null
    try {
      lists.value = await listWishLists()
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function open(wishListId: string): Promise<void> {
    loading.value = true
    loadProblem.value = null
    current.value = null
    try {
      current.value = await fetchWishList(wishListId)
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function create(
    title: string,
    description: string | null,
    targetDate: string,
    items: WishItemDraft[],
  ): Promise<WishListDetail | null> {
    problem.value = null
    try {
      const created = await apiCreate(title, description, targetDate, items)
      // Re-read rather than prepended: FR-048c puts the new list wherever its target date belongs,
      // and only the API knows that order. Guessing it here would put the list in one place until
      // the next load moved it somewhere else.
      await load()
      current.value = created
      return created
    } catch (failure) {
      problem.value = failure as ApiProblem
      return null
    }
  }

  async function remove(wishListId: string): Promise<boolean> {
    problem.value = null
    try {
      await apiDelete(wishListId)
      lists.value = lists.value.filter((list) => list.id !== wishListId)
      if (current.value?.id === wishListId) current.value = null
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /**
   * Every edit funnels through here: the API answers with the whole list, so the view is
   * re-rendered from the server's account of it rather than from a local guess about what the
   * change did (FR-036 - no edit may lose a claim that arrived meanwhile).
   */
  async function apply(change: () => Promise<WishListDetail>): Promise<boolean> {
    problem.value = null
    try {
      current.value = await change()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  function edit(patch: { title?: string; description?: string | null; targetDate?: string }) {
    return apply(() => updateWishList(current.value!.id, patch))
  }

  function addItem(item: WishItemDraft) {
    return apply(() => addWishItem(current.value!.id, item))
  }

  function editItem(itemId: string, patch: { name?: string; wantedCount?: number }) {
    return apply(() => updateWishItem(current.value!.id, itemId, patch))
  }

  async function removeItem(itemId: string): Promise<boolean> {
    const id = current.value!.id
    return apply(async () => {
      await removeWishItem(id, itemId)
      return fetchWishList(id)
    })
  }

  async function removeClaim(claimId: string): Promise<boolean> {
    const id = current.value!.id
    return apply(async () => {
      await deleteWishClaim(id, claimId)
      return fetchWishList(id)
    })
  }

  return {
    lists, current, loading, loadProblem, problem,
    load, open, create, remove, edit, addItem, editItem, removeItem, removeClaim,
  }
})

/**
 * The filled share, as a whole number, **rounded down** (research R-7).
 *
 * Rounding down is the honest direction: a list one name short must not present as finished. The
 * "complete" label is a separate comparison on the counts (see `isComplete`), so the word and the
 * number beside it cannot disagree.
 */
export function filledPercent(entryCount: number, placeCount: number): number {
  if (placeCount <= 0) return 0
  return Math.floor((entryCount / placeCount) * 100)
}

/** FR-045b: complete is a comparison of counts, never a reading of the percentage. */
export function isComplete(entryCount: number, placeCount: number): boolean {
  return placeCount > 0 && entryCount >= placeCount
}
