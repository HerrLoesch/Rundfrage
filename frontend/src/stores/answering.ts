import { defineStore } from 'pinia'
import { ref } from 'vue'
import {
  fetchOwnResponse,
  fetchPoll,
  reviseResponse,
  submitResponse,
  type ApiProblem,
  type AnswerView,
  type Availability,
  type PollView,
} from '../api/client'

/**
 * The participant's view of a poll and their own answer.
 *
 * Holds no identity of any kind: a display name is a label, and the way back to an answer is a
 * token in a link. That is Principle I expressed in state - there is simply nowhere here for an
 * account to live.
 */
export const useAnsweringStore = defineStore('answering', () => {
  const poll = ref<PollView | null>(null)
  const notFound = ref(false)
  const loading = ref(false)
  const problem = ref<ApiProblem | null>(null)

  const displayName = ref('')
  /** Day id -> availability. A day absent from this map is *no answer* (research.md R-8). */
  const answers = ref<Record<string, Availability>>({})

  const editToken = ref<string | null>(null)
  const justSubmitted = ref(false)
  const justRevised = ref(false)

  function asAnswerList(): AnswerView[] {
    return Object.entries(answers.value).map(([dayId, availability]) => ({ dayId, availability }))
  }

  function reset() {
    notFound.value = false
    problem.value = null
    justSubmitted.value = false
    justRevised.value = false
  }

  async function loadPoll(pollToken: string): Promise<void> {
    reset()
    loading.value = true
    try {
      poll.value = await fetchPoll(pollToken)
    } catch (failure) {
      const apiProblem = failure as ApiProblem
      // The neutral not-found is the only thing the server says about unknown, malformed,
      // expired and deleted alike, so there is one thing to show (SC-012).
      if (apiProblem.code === 'not_found') notFound.value = true
      else problem.value = apiProblem
    } finally {
      loading.value = false
    }
  }

  async function loadOwnResponse(token: string): Promise<void> {
    reset()
    loading.value = true
    try {
      const own = await fetchOwnResponse(token)
      poll.value = own.poll
      displayName.value = own.displayName
      answers.value = Object.fromEntries(own.answers.map((a) => [a.dayId, a.availability]))
      editToken.value = token
    } catch (failure) {
      const apiProblem = failure as ApiProblem
      if (apiProblem.code === 'not_found') notFound.value = true
      else problem.value = apiProblem
    } finally {
      loading.value = false
    }
  }

  function setAnswer(dayId: string, availability: Availability) {
    answers.value = { ...answers.value, [dayId]: availability }
  }

  async function submit(pollToken: string): Promise<boolean> {
    problem.value = null
    try {
      const accepted = await submitResponse(pollToken, displayName.value, asAnswerList())
      editToken.value = accepted.editToken
      justSubmitted.value = true
      await loadPollQuietly(pollToken)
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  async function revise(): Promise<boolean> {
    if (!editToken.value) return false
    problem.value = null
    try {
      const own = await reviseResponse(editToken.value, displayName.value, asAnswerList())
      poll.value = own.poll
      justRevised.value = true
      return true
    } catch (failure) {
      problem.value = failure as ApiProblem
      return false
    }
  }

  /** Refreshes the grid after submitting without clearing the confirmation. */
  async function loadPollQuietly(pollToken: string): Promise<void> {
    try {
      poll.value = await fetchPoll(pollToken)
    } catch {
      // The answer is stored either way; a stale grid is not worth an error message.
    }
  }

  return {
    poll, notFound, loading, problem,
    displayName, answers, editToken, justSubmitted, justRevised,
    loadPoll, loadOwnResponse, setAnswer, submit, revise,
  }
})
