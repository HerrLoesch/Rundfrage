/**
 * The operator credentials of the running instance, taken from the environment with no fallback.
 *
 * A default here would mean committing a working password to the repository - and one that
 * someone could plausibly deploy with. The application itself refuses to start without
 * credentials (002 FR-045); requiring the same of the suite that tests it is the consistent
 * choice.
 */
function required(name: string): string {
  const value = process.env[name]
  if (!value) {
    throw new Error(
      `${name} is not set. This suite needs the operator credentials of the running instance:\n` +
        '  export E2E_ADMIN_USER=... E2E_ADMIN_PASSWORD=...\n' +
        'They are whatever you put in .env - see README.md.',
    )
  }
  return value
}

export const ADMIN_USER = required('E2E_ADMIN_USER')
export const ADMIN_PASSWORD = required('E2E_ADMIN_PASSWORD')
