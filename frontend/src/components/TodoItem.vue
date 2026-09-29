<script setup lang="ts">
import { computed, ref } from 'vue'
import { ApiError } from '@/api/http'
import type { SaveTodoPayload, Todo } from '@/api/types'
import { useTodosStore } from '@/stores/todos'
import { formatDueDate, isOverdue } from '@/utils/dates'
import TodoForm from './TodoForm.vue'

const props = defineProps<{ todo: Todo }>()

const store = useTodosStore()
const editing = ref(false)
const confirmingDelete = ref(false)
const busy = ref(false)
const actionError = ref<string | null>(null)

const overdue = computed(() => isOverdue(props.todo.dueDate, props.todo.isCompleted))

async function run(action: () => Promise<void>) {
  busy.value = true
  actionError.value = null
  try {
    await action()
  } catch (error) {
    actionError.value = error instanceof ApiError ? error.message : 'Something went wrong. Please try again.'
  } finally {
    busy.value = false
    confirmingDelete.value = false
  }
}

const toggle = () => run(() => store.setCompletion(props.todo.id, !props.todo.isCompleted))
const remove = () => run(() => store.remove(props.todo.id))

async function save(payload: SaveTodoPayload) {
  await store.update(props.todo.id, payload)
  editing.value = false
}
</script>

<template>
  <li class="todo" :class="{ done: todo.isCompleted }">
    <TodoForm v-if="editing" :todo="todo" submit-label="Save changes" :on-save="save" @cancel="editing = false" />

    <template v-else>
      <input
        type="checkbox"
        class="todo-check"
        :checked="todo.isCompleted"
        :disabled="busy"
        :aria-label="`Mark ${todo.title} as ${todo.isCompleted ? 'not done' : 'done'}`"
        @change="toggle"
      />
      <div class="todo-body">
        <p class="todo-title">{{ todo.title }}</p>
        <p v-if="todo.description" class="todo-description">{{ todo.description }}</p>
        <p v-if="todo.dueDate" class="todo-meta" :class="{ overdue }">
          {{ overdue ? 'Overdue · ' : '' }}Due {{ formatDueDate(todo.dueDate) }}
        </p>
        <p v-if="actionError" class="form-error" role="alert">{{ actionError }}</p>
      </div>
      <div class="todo-actions">
        <template v-if="confirmingDelete">
          <span class="confirm-text">Delete?</span>
          <button type="button" class="btn btn-danger" :disabled="busy" @click="remove">Yes, delete</button>
          <button type="button" class="btn btn-ghost" :disabled="busy" @click="confirmingDelete = false">
            Keep
          </button>
        </template>
        <template v-else>
          <button type="button" class="btn btn-ghost" :disabled="busy" @click="editing = true">Edit</button>
          <button type="button" class="btn btn-ghost" :disabled="busy" @click="confirmingDelete = true">
            Delete
          </button>
        </template>
      </div>
    </template>
  </li>
</template>
