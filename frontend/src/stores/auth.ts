import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { authApi } from '@/api/auth'
import type { AuthResponse, User } from '@/api/types'

const STORAGE_KEY = 'todo-app.session'

interface Session {
  token: string
  expiresAt: string
  user: User
}

function loadSession(): Session | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) return null
    const session = JSON.parse(raw) as Session
    return isExpired(session.expiresAt) ? null : session
  } catch {
    return null
  }
}

function isExpired(expiresAt: string): boolean {
  return new Date(expiresAt).getTime() <= Date.now()
}

/**
 * Holds the signed-in user's session. The JWT lives in localStorage so a refresh keeps you signed in; see the
 * README for the trade-off versus an httpOnly cookie.
 */
export const useAuthStore = defineStore('auth', () => {
  const session = ref<Session | null>(loadSession())

  const token = computed(() => session.value?.token ?? null)
  const user = computed(() => session.value?.user ?? null)
  const isAuthenticated = computed(() => session.value !== null && !isExpired(session.value.expiresAt))

  function setSession(response: AuthResponse) {
    session.value = { token: response.accessToken, expiresAt: response.expiresAt, user: response.user }
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(session.value))
    } catch {
      // Storage unavailable (private mode / quota): the session simply won't survive a reload.
    }
  }

  async function login(email: string, password: string) {
    setSession(await authApi.login(email, password))
  }

  async function register(email: string, password: string) {
    setSession(await authApi.register(email, password))
  }

  function logout() {
    session.value = null
    try {
      localStorage.removeItem(STORAGE_KEY)
    } catch {
      // ignore
    }
  }

  return { token, user, isAuthenticated, login, register, logout }
})
