import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError, configureHttp, request } from './http'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } })
}

describe('http client', () => {
  const onUnauthorized = vi.fn()
  const fetchMock = vi.fn<typeof fetch>()

  beforeEach(() => {
    fetchMock.mockReset()
    onUnauthorized.mockReset()
    vi.stubGlobal('fetch', fetchMock)
    configureHttp({ getToken: () => 'test-token', onUnauthorized })
  })

  it('sends the bearer token and JSON body, and parses the JSON response', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ ok: true }))

    const result = await request<{ ok: boolean }>('/api/things', { method: 'POST', body: { a: 1 } })

    expect(result).toEqual({ ok: true })
    const [url, init] = fetchMock.mock.calls[0]!
    expect(String(url)).toContain('/api/things')
    expect(init?.headers).toMatchObject({ Authorization: 'Bearer test-token', 'Content-Type': 'application/json' })
    expect(init?.body).toBe(JSON.stringify({ a: 1 }))
  })

  it('omits the token for unauthenticated calls', async () => {
    fetchMock.mockResolvedValue(jsonResponse({}))

    await request('/api/auth/login', { method: 'POST', body: {}, authenticated: false })

    expect(fetchMock.mock.calls[0]![1]?.headers).not.toHaveProperty('Authorization')
  })

  it('builds the query string and skips empty values', async () => {
    fetchMock.mockResolvedValue(jsonResponse({}))

    await request('/api/todos', { query: { status: 'active', search: '', page: 2, pageSize: undefined } })

    const url = new URL(String(fetchMock.mock.calls[0]![0]))
    expect(url.searchParams.get('status')).toBe('active')
    expect(url.searchParams.get('page')).toBe('2')
    expect(url.searchParams.has('search')).toBe(false)
    expect(url.searchParams.has('pageSize')).toBe(false)
  })

  it('returns undefined for 204 No Content', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }))

    await expect(request('/api/todos/1', { method: 'DELETE' })).resolves.toBeUndefined()
  })

  it('surfaces the problem detail and notifies on 401 for authenticated calls', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ title: 'Unauthorized' }, 401))

    await expect(request('/api/todos')).rejects.toMatchObject({ status: 401 })
    expect(onUnauthorized).toHaveBeenCalledOnce()
  })

  it('does not treat a 401 from login as an expired session', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ detail: 'Invalid email or password.' }, 401))

    await expect(request('/api/auth/login', { method: 'POST', body: {}, authenticated: false })).rejects.toThrow(
      'Invalid email or password.',
    )
    expect(onUnauthorized).not.toHaveBeenCalled()
  })

  it('exposes validation errors by field', async () => {
    fetchMock.mockResolvedValue(jsonResponse({ title: 'Validation', errors: { Title: ['Title is required.'] } }, 400))

    const error = await request('/api/todos', { method: 'POST', body: {} }).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(ApiError)
    expect((error as ApiError).message).toBe('Title is required.')
    expect((error as ApiError).fieldErrors).toEqual({ Title: ['Title is required.'] })
  })

  it('gives a friendly message for rate limiting, even with an empty body', async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 429 }))

    await expect(request('/api/auth/login', { authenticated: false })).rejects.toThrow(/too many attempts/i)
  })

  it('wraps network failures in an ApiError with status 0', async () => {
    fetchMock.mockRejectedValue(new TypeError('Failed to fetch'))

    await expect(request('/api/todos')).rejects.toMatchObject({ status: 0 })
  })
})
