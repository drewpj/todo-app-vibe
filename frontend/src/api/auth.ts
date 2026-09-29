import { request } from './http'
import type { AuthResponse } from './types'

export const authApi = {
  register: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/register', { method: 'POST', body: { email, password }, authenticated: false }),

  login: (email: string, password: string) =>
    request<AuthResponse>('/api/auth/login', { method: 'POST', body: { email, password }, authenticated: false }),
}
