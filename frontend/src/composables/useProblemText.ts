import { useI18n } from 'vue-i18n'
import type { ApiProblem } from '../api/client'

/**
 * Turns a machine-readable problem from the API into German (FR-029).
 *
 * Unknown codes fall back to a generic message rather than rendering the raw code: a user should
 * never be shown `title_too_long`, and a missing translation must not look like a crash.
 */
export function useProblemText() {
  const { t, te } = useI18n()

  return (problem: ApiProblem | null): string => {
    if (!problem) return ''

    const key = `error.${problem.code}`
    if (!te(key)) return t('error.unexpected')

    return t(key, {
      limit: problem.limit ?? 0,
      minutes: Math.ceil((problem.retryAfterSeconds ?? 0) / 60),
    })
  }
}
