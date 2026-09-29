<script setup lang="ts">
import { ref } from 'vue'
import { ApiError } from '@/api/http'
import type { SaveTodoPayload, Todo } from '@/api/types'
import { toApiDate, toDateInput } from '@/utils/dates'
import FormField from './FormField.vue'

const TITLE_MAX = 200
const DESCRIPTION_MAX = 2000

const props = defineProps<{
  /** Pre-fills the form when editing an existing todo. */
  todo?: Todo
  submitLabel: string
  /** Quick-add layout: just a title row, with description and due date behind a "Details" toggle. */
  compact?: boolean
  /** Performs the save. Rejecting with an ApiError shows its message in the form. */
  onSave: (payload: SaveTodoPayload) => Promise<void>
}>()

const emit = defineEmits<{ cancel: [] }>()

const title = ref(props.todo?.title ?? '')
const description = ref(props.todo?.description ?? '')
const dueDate = ref(toDateInput(props.todo?.dueDate ?? null))
const titleError = ref<string | null>(null)
const formError = ref<string | null>(null)
const submitting = ref(false)
const showDetails = ref(!props.compact)

async function submit() {
  if (submitting.value) return
  titleError.value = null
  formError.value = null

  const trimmedTitle = title.value.trim()
  if (!trimmedTitle) {
    titleError.value = 'Please enter a title.'
    return
  }

  const submitted = { title: title.value, description: description.value, dueDate: dueDate.value }
  submitting.value = true
  try {
    await props.onSave({
      title: trimmedTitle,
      description: description.value.trim() || null,
      dueDate: toApiDate(dueDate.value),
    })
    if (!props.todo) resetIfUnchanged(submitted)
  } catch (error) {
    formError.value = error instanceof ApiError ? error.message : 'Could not save the task. Please try again.'
  } finally {
    submitting.value = false
  }
}

/** Clears the create form, but never discards text the user has started typing for the *next* task. */
function resetIfUnchanged(submitted: { title: string; description: string; dueDate: string }) {
  if (title.value === submitted.title) title.value = ''
  if (description.value === submitted.description) description.value = ''
  if (dueDate.value === submitted.dueDate) dueDate.value = ''
  if (!title.value && !description.value && !dueDate.value) showDetails.value = !props.compact
}
</script>

<template>
  <form class="todo-form" :class="{ compact }" novalidate @submit.prevent="submit">
    <div :class="{ 'quick-row': compact }">
      <FormField
        v-model="title"
        :label="compact ? 'New task title' : 'Title'"
        :hide-label="compact"
        :maxlength="TITLE_MAX"
        :error="titleError"
      />
      <div v-if="compact" class="quick-actions">
        <button type="submit" class="btn btn-primary" :disabled="submitting">
          {{ submitting ? 'Adding…' : submitLabel }}
        </button>
        <button
          type="button"
          class="btn btn-ghost"
          :aria-expanded="showDetails"
          @click="showDetails = !showDetails"
        >
          {{ showDetails ? 'Hide details' : 'Details' }}
        </button>
      </div>
    </div>

    <template v-if="showDetails">
      <FormField v-model="description" label="Description (optional)" multiline :maxlength="DESCRIPTION_MAX" />
      <FormField v-model="dueDate" label="Due date (optional)" type="date" />
    </template>

    <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

    <div v-if="!compact" class="form-actions">
      <button type="submit" class="btn btn-primary" :disabled="submitting">
        {{ submitting ? 'Saving…' : submitLabel }}
      </button>
      <button v-if="todo" type="button" class="btn btn-ghost" :disabled="submitting" @click="emit('cancel')">
        Cancel
      </button>
    </div>
  </form>
</template>
