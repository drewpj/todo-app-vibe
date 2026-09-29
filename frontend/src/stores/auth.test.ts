import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { authApi } from '@/api/auth'
import type { AuthResponse } from '@/api/types'
import { useAuthStore } from './auth'

vi.mock('@/api/auth')

const STORAGE_KEY = 'todo-app.session'

function authResponse(expiresInMs: number): AuthResponse {
  return {
    accessToken: 'jwt-token',
    expiresAt: new Date(Date.now() + expiresInMs).toISOString(),
    user: { id: 'u1', email: 'a@example.com' },
  }
}

describe('auth store', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('starts signed out', () => {
    const auth = useAuthStore()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.token).toBeNull()
  })

  it('stores the session on login and persists it', async () => {
    vi.mocked(authApi.login).mockResolvedValue(authResponse(60_000))
    const auth = useAuthStore()

    await auth.login('a@example.com', 'password123')

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.token).toBe('jwt-token')
    expect(auth.user?.email).toBe('a@example.com')
    expect(localStorage.getItem(STORAGE_KEY)).toContain('jwt-token')
  })

  it('stores the session on register', async () => {
    vi.mocked(authApi.register).mockResolvedValue(authResponse(60_000))
    const auth = useAuthStore()

    await auth.register('a@example.com', 'password123')

    expect(auth.isAuthenticated).toBe(true)
  })

  it('stays signed out when login fails', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new Error('nope'))
    const auth = useAuthStore()

    await expect(auth.login('a@example.com', 'bad')).rejects.toThrow('nope')

    expect(auth.isAuthenticated).toBe(false)
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
  })

  it('restores a valid session from storage', () => {
    const response = authResponse(60_000)
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: response.accessToken, expiresAt: response.expiresAt, user: response.user }),
    )

    const auth = useAuthStore()

    expect(auth.isAuthenticated).toBe(true)
    expect(auth.user?.id).toBe('u1')
  })

  it('discards an expired stored session', () => {
    const response = authResponse(-1_000)
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ token: response.accessToken, expiresAt: response.expiresAt, user: response.user }),
    )

    expect(useAuthStore().isAuthenticated).toBe(false)
  })

  it('ignores corrupt stored data', () => {
    localStorage.setItem(STORAGE_KEY, '{not json')

    expect(useAuthStore().isAuthenticated).toBe(false)
  })

  it('clears memory and storage on logout', async () => {
    vi.mocked(authApi.login).mockResolvedValue(authResponse(60_000))
    const auth = useAuthStore()
    await auth.login('a@example.com', 'password123')

    auth.logout()

    expect(auth.isAuthenticated).toBe(false)
    expect(auth.token).toBeNull()
    expect(localStorage.getItem(STORAGE_KEY)).toBeNull()
  })
})
