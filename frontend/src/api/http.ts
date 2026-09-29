/**
 * Minimal fetch wrapper: JSON in/out, bearer token injection, RFC 7807 error parsing, and a single hook for
 * "the server says our session is no longer valid".
 */

const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
    /** Field -> messages, present for 400 validation problems. */
    public readonly fieldErrors: Record<string, string[]> = {},
  ) {
    super(message)
    this.name = 'ApiError'
  }
}

type TokenProvider = () => string | null
type UnauthorizedHandler = () => void

let getToken: TokenProvider = () => null
let onUnauthorized: UnauthorizedHandler = () => {}

/** Wired up once by the auth store; keeps this module free of any store/router imports. */
export function configureHttp(options: { getToken: TokenProvider; onUnauthorized: UnauthorizedHandler }): void {
  getToken = options.getToken
  onUnauthorized = options.onUnauthorized
}

interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: Record<string, string | number | undefined>
  /** Set false for login/register, where a 401 means "wrong credentials", not "session expired". */
  authenticated?: boolean
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, authenticated = true } = options

  const url = new URL(BASE_URL + path, window.location.origin)
  for (const [key, value] of Object.entries(query ?? {})) {
    if (value !== undefined && value !== '') url.searchParams.set(key, String(value))
  }

  const headers: Record<string, string> = { Accept: 'application/json' }
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  const token = authenticated ? getToken() : null
  if (token) headers.Authorization = `Bearer ${token}`

  let response: Response
  try {
    response = await fetch(url, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) })
  } catch {
    throw new ApiError(0, 'Could not reach the server. Check your connection and try again.')
  }

  if (response.status === 401 && authenticated) onUnauthorized()
  if (!response.ok) throw await toApiError(response)

  return (response.status === 204 ? undefined : await response.json()) as T
}

async function toApiError(response: Response): Promise<ApiError> {
  let problem: { title?: string; detail?: string; errors?: Record<string, string[]> } = {}
  try {
    problem = await response.json()
  } catch {
    // Non-JSON error body (e.g. a proxy error page) - fall through to the generic message.
  }

  const fieldErrors = problem.errors ?? {}
  const firstFieldError = Object.values(fieldErrors)[0]?.[0]
  const message =
    problem.detail ??
    firstFieldError ??
    (response.status === 429 ? 'Too many attempts. Please wait a moment and try again.' : undefined) ??
    problem.title ??
    `Request failed (${response.status})`

  return new ApiError(response.status, message, fieldErrors)
}
