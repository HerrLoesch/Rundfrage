import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  createCreator as apiCreate,
  deleteCreator as apiDelete,
  listCreators,
  reissueCreatorLink,
  renameCreator,
  revokeCreatorLink,
  type ApiProblem,
  type CreatorSummary,
} from '../api/client'

/**
 * The operator's view of Ersteller (009 FR-044 to FR-046).
 *
 * Two problem fields rather than one, for the reason the poll and wish-list stores record: reading
 * the list and writing to it fail for unrelated reasons, and the interface says different things
 * about them. A duplicate name belongs in the form; an unreachable store belongs above the list.
 */
export const useCreatorsStore = defineStore('creators', () => {
  const creators = ref<CreatorSummary[]>([])
  const loading = ref(false)

  const loadProblem = ref<ApiProblem | null>(null)
  const problem = ref<ApiProblem | null>(null)

  async function load(): Promise<void> {
    loading.value = true
    loadProblem.value = null
    try {
      creators.value = await listCreators()
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function create(name: string): Promise<CreatorSummary | null> {
    problem.value = null
    try {
      const created = await apiCreate(name)
      await load()
      return created
    } catch (failure) {
      problem.value = failure as ApiProblem
      return null
    }
  }

  async function rename(creatorId: string, name: string): Promise<boolean> {
    problem.value = null
    try {
      await renameCreator(creatorId, name)
      await load()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /**
   * A new link. The previous one stops working; everything the Ersteller owns stays theirs.
   *
   * This is also how a revoked Ersteller is given access again (FR-016, FR-020).
   */
  async function reissue(creatorId: string): Promise<boolean> {
    problem.value = null
    try {
      await reissueCreatorLink(creatorId)
      await load()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /** Takes the link away and destroys nothing (FR-018, FR-020). */
  async function revoke(creatorId: string): Promise<boolean> {
    problem.value = null
    try {
      await revokeCreatorLink(creatorId)
      await load()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /** Destroys the Ersteller together with every poll and wish list it owns (FR-020a). */
  async function remove(creatorId: string): Promise<boolean> {
    problem.value = null
    try {
      await apiDelete(creatorId)
      await load()
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  return { creators, loading, loadProblem, problem, load, create, rename, reissue, revoke, remove }
})
