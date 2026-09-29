import { request } from './http'
import type { Paged, SaveTodoPayload, Todo, TodoQuery } from './types'

export const todosApi = {
  list: (query: TodoQuery) =>
    request<Paged<Todo>>('/api/todos', {
      query: {
        status: query.status === 'all' ? undefined : query.status,
        search: query.search.trim() || undefined,
        page: query.page,
        pageSize: query.pageSize,
      },
    }),

  create: (payload: SaveTodoPayload) => request<Todo>('/api/todos', { method: 'POST', body: payload }),

  update: (id: string, payload: SaveTodoPayload) =>
    request<Todo>(`/api/todos/${id}`, { method: 'PUT', body: payload }),

  setCompletion: (id: string, isCompleted: boolean) =>
    request<Todo>(`/api/todos/${id}/completion`, { method: 'PATCH', body: { isCompleted } }),

  remove: (id: string) => request<void>(`/api/todos/${id}`, { method: 'DELETE' }),
}
