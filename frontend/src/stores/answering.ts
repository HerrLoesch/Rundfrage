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

  /**
   * How the poll on screen was reached, so that paging can ask the same source again. Held here
   * rather than passed in, because this is the only place that knows it.
   */
  const source = ref<{ kind: 'poll' | 'response'; token: string } | null>(null)
  const problem = ref<ApiProblem | null>(null)

  /**
   * Maintenance is its own state rather than one more error. It is not a failure - the operator
   * switched it on deliberately - and the participant is owed a notice, not an error message
   * (005 FR-026, FR-032).
   */
  const maintenance = ref(false)

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
    maintenance.value = false
    justSubmitted.value = false
    justRevised.value = false
  }

  /**
   * Clears everything belonging to one participant's answer.
   *
   * The store is a singleton, so without this, opening a second poll link in the same session
   * would carry the previous poll's name, answers and edit token across - and a save would then
   * revise a response belonging to a different poll entirely.
   */
  function forgetAnswer() {
    displayName.value = ''
    answers.value = {}
    editToken.value = null
  }

  async function loadPoll(pollToken: string): Promise<void> {
    reset()
    forgetAnswer()
    source.value = { kind: 'poll', token: pollToken }
    loading.value = true
    try {
      poll.value = await fetchPoll(pollToken)
    } catch (failure) {
      const apiProblem = failure as ApiProblem
      // The neutral not-found is the only thing the server says about unknown, malformed,
      // expired and deleted alike, so there is one thing to show (SC-012).
      if (apiProblem.code === 'not_found') notFound.value = true
      else if (apiProblem.code === 'maintenance') maintenance.value = true
      else problem.value = apiProblem
    } finally {
      loading.value = false
    }
  }

  async function loadOwnResponse(token: string): Promise<void> {
    reset()
    source.value = { kind: 'response', token }
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
      else if (apiProblem.code === 'maintenance') maintenance.value = true
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

  /**
   * Fetches another page of the grid.
   *
   * Two things this deliberately is not. It is not `loadPoll`, which resets the store and forgets
   * the answer in progress - reading page two of the results must not throw away a half-filled
   * form. And it takes no token: the grid is reached through two different links, and asking the
   * component to remember which one it came through would put that knowledge in the one place
   * that does not need it. The store already knows, because it did the loading.
   *
   * Paging is a read of the grid below the form, so it adds nothing between the link and the
   * answer form (Principle I, 002 research R-7).
   */
  async function changePage(page: number): Promise<void> {
    if (!source.value) return

    try {
      poll.value =
        source.value.kind === 'poll'
          ? await fetchPoll(source.value.token, page)
          : (await fetchOwnResponse(source.value.token, page)).poll
    } catch (failure) {
      problem.value = failure as ApiProblem
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
    poll, notFound, loading, problem, maintenance,
    displayName, answers, editToken, justSubmitted, justRevised,
    loadPoll, loadOwnResponse, changePage, setAnswer, submit, revise,
  }
})
