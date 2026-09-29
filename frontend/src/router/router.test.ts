import { beforeEach, describe, expect, it } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { createAppRouter } from './index'

function signIn() {
  localStorage.setItem(
    'todo-app.session',
    JSON.stringify({
      token: 't',
      expiresAt: new Date(Date.now() + 60_000).toISOString(),
      user: { id: 'u', email: 'a@example.com' },
    }),
  )
}

describe('router guards', () => {
  beforeEach(() => {
    localStorage.clear()
    setActivePinia(createPinia())
  })

  it('redirects anonymous users from a protected route to login', async () => {
    const router = createAppRouter(createMemoryHistory())

    await router.push('/')

    expect(router.currentRoute.value.name).toBe('login')
  })

  it('lets signed-in users reach the todo list', async () => {
    signIn()
    setActivePinia(createPinia())
    const router = createAppRouter(createMemoryHistory())

    await router.push('/')

    expect(router.currentRoute.value.name).toBe('todos')
  })

  it('keeps signed-in users away from login and register', async () => {
    signIn()
    setActivePinia(createPinia())
    const router = createAppRouter(createMemoryHistory())

    await router.push('/login')
    expect(router.currentRoute.value.name).toBe('todos')

    await router.push('/register')
    expect(router.currentRoute.value.name).toBe('todos')
  })

  it('sends users back to login once they sign out', async () => {
    signIn()
    setActivePinia(createPinia())
    const router = createAppRouter(createMemoryHistory())
    await router.push('/')

    useAuthStore().logout()
    await router.push('/?refresh=1')

    expect(router.currentRoute.value.name).toBe('login')
  })

  it('sends unknown paths to the todo list (and then to login if signed out)', async () => {
    const router = createAppRouter(createMemoryHistory())

    await router.push('/does/not/exist')

    expect(router.currentRoute.value.name).toBe('login')
  })
})
