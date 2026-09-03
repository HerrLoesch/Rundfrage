import { defineStore } from 'pinia'
import { ref } from 'vue'
import { createPoll as apiCreate, listPolls, type ApiProblem, type PollSummary } from '../api/client'

export const usePollsStore = defineStore('polls', () => {
  const polls = ref<PollSummary[]>([])
  const created = ref<PollSummary | null>(null)
  const problem = ref<ApiProblem | null>(null)
  const loading = ref(false)

  async function load(): Promise<void> {
    loading.value = true
    problem.value = null
    try {
      polls.value = await listPolls()
    } catch (failure) {
      problem.value = failure as ApiProblem
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

  return { polls, created, problem, loading, load, create }
})
