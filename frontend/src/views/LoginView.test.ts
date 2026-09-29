import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory } from 'vue-router'
import { ApiError } from '@/api/http'
import { authApi } from '@/api/auth'
import { createAppRouter } from '@/router'
import LoginView from './LoginView.vue'

vi.mock('@/api/auth')

async function setup(initialPath = '/login') {
  const pinia = createPinia()
  setActivePinia(pinia)
  const router = createAppRouter(createMemoryHistory())
  await router.push(initialPath)
  await router.isReady()
  const wrapper = mount(LoginView, { global: { plugins: [pinia, router] } })
  return { wrapper, router }
}

async function fillAndSubmit(wrapper: ReturnType<typeof mount>, email: string, password: string) {
  await wrapper.find('input[type="email"]').setValue(email)
  await wrapper.find('input[type="password"]').setValue(password)
  await wrapper.find('form').trigger('submit')
  await flushPromises()
}

const session = {
  accessToken: 'jwt',
  expiresAt: new Date(Date.now() + 60_000).toISOString(),
  user: { id: 'u1', email: 'a@example.com' },
}

describe('LoginView', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('signs in and navigates to the todo list', async () => {
    vi.mocked(authApi.login).mockResolvedValue(session)
    const { wrapper, router } = await setup()

    await fillAndSubmit(wrapper, 'a@example.com', 'password123')

    expect(authApi.login).toHaveBeenCalledWith('a@example.com', 'password123')
    await vi.waitFor(() => expect(router.currentRoute.value.name).toBe('todos'))
  })

  it('shows the server message on invalid credentials and stays on the page', async () => {
    vi.mocked(authApi.login).mockRejectedValue(new ApiError(401, 'Invalid email or password.'))
    const { wrapper, router } = await setup()

    await fillAndSubmit(wrapper, 'a@example.com', 'wrong')

    expect(wrapper.find('[role="alert"]').text()).toBe('Invalid email or password.')
    expect(router.currentRoute.value.name).toBe('login')
  })

  it('does not call the API when fields are empty', async () => {
    const { wrapper } = await setup()

    await fillAndSubmit(wrapper, '', '')

    expect(authApi.login).not.toHaveBeenCalled()
    expect(wrapper.find('[role="alert"]').exists()).toBe(true)
  })

  it('returns to the originally requested page after login', async () => {
    vi.mocked(authApi.login).mockResolvedValue(session)
    const { wrapper, router } = await setup('/login?redirect=/')

    await fillAndSubmit(wrapper, 'a@example.com', 'password123')

    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/'))
  })

  it('ignores redirect targets that leave the site', async () => {
    vi.mocked(authApi.login).mockResolvedValue(session)
    const { wrapper, router } = await setup('/login?redirect=//evil.example.com')

    await fillAndSubmit(wrapper, 'a@example.com', 'password123')

    await vi.waitFor(() => expect(router.currentRoute.value.fullPath).toBe('/'))
  })
})
