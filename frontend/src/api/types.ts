/** Wire types. These mirror the API contracts in backend/src/TodoApp.Api/Contracts. */

export interface User {
  id: string
  email: string
}

export interface AuthResponse {
  accessToken: string
  /** ISO-8601 UTC instant. */
  expiresAt: string
  user: User
}

export interface Todo {
  id: string
  title: string
  description: string | null
  isCompleted: boolean
  completedAt: string | null
  dueDate: string | null
  createdAt: string
  updatedAt: string
}

export interface SaveTodoPayload {
  title: string
  description: string | null
  dueDate: string | null
}

export type TodoStatus = 'all' | 'active' | 'completed'

export interface TodoQuery {
  status: TodoStatus
  search: string
  page: number
  pageSize: number
}

export interface Paged<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}
