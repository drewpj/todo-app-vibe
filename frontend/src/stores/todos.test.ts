import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { ApiError } from '@/api/http'
import { todosApi } from '@/api/todos'
import type { Paged, Todo } from '@/api/types'
import { useTodosStore } from './todos'

vi.mock('@/api/todos')

function todo(id: string, overrides: Partial<Todo> = {}): Todo {
  return {
    id,
    title: `Todo ${id}`,
    description: null,
    isCompleted: false,
    completedAt: null,
    dueDate: null,
    createdAt: '2030-01-01T00:00:00Z',
    updatedAt: '2030-01-01T00:00:00Z',
    ...overrides,
  }
}

function page(items: Todo[], totalCount = items.length, pageNumber = 1): Paged<Todo> {
  return { items, page: pageNumber, pageSize: 10, totalCount }
}

describe('todos store', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(todosApi.list).mockResolvedValue(page([]))
  })

  it('loads items and total count', async () => {
    vi.mocked(todosApi.list).mockResolvedValue(page([todo('1'), todo('2')], 25))
    const store = useTodosStore()

    await store.load()

    expect(store.items).toHaveLength(2)
    expect(store.totalCount).toBe(25)
    expect(store.totalPages).toBe(3)
    expect(store.loading).toBe(false)
  })

  it('exposes the error message when loading fails', async () => {
    vi.mocked(todosApi.list).mockRejectedValue(new ApiError(500, 'Server error'))
    const store = useTodosStore()

    await store.load()

    expect(store.loadError).toBe('Server error')
    expect(store.loading).toBe(false)
  })

  it('sends the current filters to the API and resets to page 1 when they change', async () => {
    const store = useTodosStore()
    store.query.page = 3

    await store.setStatus('active')
    await store.setSearch('milk')

    expect(todosApi.list).toHaveBeenLastCalledWith(expect.objectContaining({ status: 'active', search: 'milk', page: 1 }))
  })

  it('ignores a stale response that arrives after a newer request', async () => {
    let resolveSlow!: (value: Paged<Todo>) => void
    vi.mocked(todosApi.list)
      .mockImplementationOnce(() => new Promise((resolve) => (resolveSlow = resolve)))
      .mockResolvedValueOnce(page([todo('fresh')]))
    const store = useTodosStore()

    const slow = store.load()
    await store.load()
    resolveSlow(page([todo('stale')]))
    await slow

    expect(store.items.map((t) => t.id)).toEqual(['fresh'])
  })

  it('steps back a page when the current page becomes empty', async () => {
    vi.mocked(todosApi.list)
      .mockResolvedValueOnce(page([], 10, 2))
      .mockResolvedValueOnce(page([todo('1')], 10, 1))
    const store = useTodosStore()
    store.query.page = 2

    await store.load()

    expect(store.query.page).toBe(1)
    expect(store.items).toHaveLength(1)
  })

  it('clamps page navigation to the valid range', async () => {
    vi.mocked(todosApi.list).mockResolvedValue(page([todo('1')], 25))
    const store = useTodosStore()
    await store.load()

    await store.goToPage(99)
    expect(store.query.page).toBe(3)

    await store.goToPage(0)
    expect(store.query.page).toBe(1)
  })

  it('reloads the list after each mutation', async () => {
    vi.mocked(todosApi.create).mockResolvedValue(todo('n'))
    vi.mocked(todosApi.update).mockResolvedValue(todo('n'))
    vi.mocked(todosApi.setCompletion).mockResolvedValue(todo('n'))
    vi.mocked(todosApi.remove).mockResolvedValue()
    const store = useTodosStore()

    await store.create({ title: 't', description: null, dueDate: null })
    await store.update('n', { title: 't2', description: null, dueDate: null })
    await store.setCompletion('n', true)
    await store.remove('n')

    expect(todosApi.list).toHaveBeenCalledTimes(4)
  })

  it('propagates mutation errors to the caller', async () => {
    vi.mocked(todosApi.remove).mockRejectedValue(new ApiError(404, 'Todo not found.'))
    const store = useTodosStore()

    await expect(store.remove('missing')).rejects.toThrow('Todo not found.')
  })

  it('clears everything on reset', async () => {
    vi.mocked(todosApi.list).mockResolvedValue(page([todo('1')]))
    const store = useTodosStore()
    await store.load()
    store.query.status = 'completed'

    store.reset()

    expect(store.items).toEqual([])
    expect(store.query.status).toBe('all')
  })
})
