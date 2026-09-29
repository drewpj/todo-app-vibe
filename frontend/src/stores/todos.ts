import { computed, reactive, ref } from 'vue'
import { defineStore } from 'pinia'
import { ApiError } from '@/api/http'
import { todosApi } from '@/api/todos'
import type { SaveTodoPayload, Todo, TodoQuery, TodoStatus } from '@/api/types'

export const PAGE_SIZE = 10

function messageOf(error: unknown): string {
  return error instanceof ApiError ? error.message : 'Something went wrong. Please try again.'
}

export const useTodosStore = defineStore('todos', () => {
  const items = ref<Todo[]>([])
  const totalCount = ref(0)
  const loading = ref(false)
  const loadError = ref<string | null>(null)
  const query = reactive<TodoQuery>({ status: 'all', search: '', page: 1, pageSize: PAGE_SIZE })

  const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / query.pageSize)))

  // Guards against out-of-order responses (e.g. fast typing in search): only the latest request may write state.
  let latestRequest = 0

  async function load() {
    const requestId = ++latestRequest
    loading.value = true
    loadError.value = null
    try {
      const result = await todosApi.list({ ...query })
      if (requestId !== latestRequest) return

      // The last item on the last page was removed/moved elsewhere: step back instead of showing an empty page.
      if (result.items.length === 0 && result.totalCount > 0 && query.page > 1) {
        query.page = Math.max(1, Math.ceil(result.totalCount / query.pageSize))
        return load()
      }

      items.value = result.items
      totalCount.value = result.totalCount
    } catch (error) {
      if (requestId !== latestRequest) return
      loadError.value = messageOf(error)
    } finally {
      if (requestId === latestRequest) loading.value = false
    }
  }

  function setStatus(status: TodoStatus) {
    query.status = status
    query.page = 1
    return load()
  }

  function setSearch(search: string) {
    query.search = search
    query.page = 1
    return load()
  }

  function goToPage(page: number) {
    query.page = Math.min(Math.max(1, page), totalPages.value)
    return load()
  }

  // Mutations throw ApiError so the calling component can show the message next to the form that caused it.
  async function create(payload: SaveTodoPayload) {
    await todosApi.create(payload)
    query.page = 1
    await load()
  }

  async function update(id: string, payload: SaveTodoPayload) {
    await todosApi.update(id, payload)
    await load()
  }

  async function setCompletion(id: string, isCompleted: boolean) {
    await todosApi.setCompletion(id, isCompleted)
    await load()
  }

  async function remove(id: string) {
    await todosApi.remove(id)
    await load()
  }

  function reset() {
    latestRequest++
    items.value = []
    totalCount.value = 0
    loadError.value = null
    loading.value = false
    Object.assign(query, { status: 'all', search: '', page: 1 })
  }

  return {
    items,
    totalCount,
    totalPages,
    loading,
    loadError,
    query,
    load,
    setStatus,
    setSearch,
    goToPage,
    create,
    update,
    setCompletion,
    remove,
    reset,
  }
})
