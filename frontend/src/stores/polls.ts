import { defineStore } from 'pinia'
import { ref } from 'vue'
import { createPoll as apiCreate, listPolls, type ApiProblem, type PollSummary } from '../api/client'

export const usePollsStore = defineStore('polls', () => {
  const polls = ref<PollSummary[]>([])
  const created = ref<PollSummary | null>(null)
  const loading = ref(false)

  /**
   * Why two problem fields and not one.
   *
   * Reading the list and creating a poll fail for unrelated reasons, and the interface says
   * different things about them: a rejected title belongs in the form, an unreadable store
   * belongs above the list. Sharing one field meant a poll submitted without a title produced
   * the validation message *and* "your data cannot be reached" - two accounts of one event, one
   * of them false and alarming.
   */
  const loadProblem = ref<ApiProblem | null>(null)
  const problem = ref<ApiProblem | null>(null)

  async function load(): Promise<void> {
    loading.value = true
    loadProblem.value = null
    try {
      polls.value = await listPolls()
    } catch (failure) {
      loadProblem.value = failure as ApiProblem
    } finally {
      loading.value = false
    }
  }

  async function create(title: string, message: string | null, days: string[]): Promise<boolean> {
    problem.value = null
    try {
      created.value = await apiCreate(title, message, days)
      polls.value = [created.value, ...polls.value]
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  return { polls, created, loadProblem, problem, loading, load, create }
})
