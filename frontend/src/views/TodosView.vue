<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import type { SaveTodoPayload } from '@/api/types'
import PaginationBar from '@/components/PaginationBar.vue'
import TodoFilters from '@/components/TodoFilters.vue'
import TodoForm from '@/components/TodoForm.vue'
import TodoItem from '@/components/TodoItem.vue'
import { useTodosStore } from '@/stores/todos'

const SEARCH_DEBOUNCE_MS = 300

const store = useTodosStore()
const searchText = ref(store.query.search)
let searchTimer: ReturnType<typeof setTimeout> | undefined

const isFiltered = computed(() => store.query.status !== 'all' || store.query.search.trim() !== '')
const showInitialLoading = computed(() => store.loading && store.items.length === 0 && !store.loadError)

onMounted(() => store.load())
onBeforeUnmount(() => clearTimeout(searchTimer))

function onSearchInput(value: string) {
  searchText.value = value
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => store.setSearch(value), SEARCH_DEBOUNCE_MS)
}

const addTodo = (payload: SaveTodoPayload) => store.create(payload)
</script>

<template>
  <section class="panel">
    <h1>My tasks</h1>
    <TodoForm compact submit-label="Add task" :on-save="addTodo" />
  </section>

  <section class="panel">
    <TodoFilters
      :status="store.query.status"
      :search="searchText"
      @update:status="store.setStatus"
      @update:search="onSearchInput"
    />

    <p v-if="showInitialLoading" class="state-message" role="status">Loading tasks…</p>

    <div v-else-if="store.loadError" class="state-message" role="alert">
      <p>{{ store.loadError }}</p>
      <button type="button" class="btn btn-primary" @click="store.load()">Try again</button>
    </div>

    <p v-else-if="store.items.length === 0" class="state-message">
      {{ isFiltered ? 'No tasks match your filters.' : 'No tasks yet. Add your first one above.' }}
    </p>

    <ul v-else class="todo-list" :aria-busy="store.loading">
      <TodoItem v-for="todo in store.items" :key="todo.id" :todo="todo" />
    </ul>

    <PaginationBar :page="store.query.page" :total-pages="store.totalPages" @change="store.goToPage" />
  </section>
</template>
