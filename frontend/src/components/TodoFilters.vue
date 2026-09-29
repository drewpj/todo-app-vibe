<script setup lang="ts">
import type { TodoStatus } from '@/api/types'

const options: { value: TodoStatus; label: string }[] = [
  { value: 'all', label: 'All' },
  { value: 'active', label: 'Active' },
  { value: 'completed', label: 'Completed' },
]

defineProps<{ status: TodoStatus }>()
const search = defineModel<string>('search', { required: true })
const emit = defineEmits<{ 'update:status': [status: TodoStatus] }>()
</script>

<template>
  <div class="filters">
    <div class="segmented" role="group" aria-label="Filter by status">
      <button
        v-for="option in options"
        :key="option.value"
        type="button"
        :class="{ active: status === option.value }"
        :aria-pressed="status === option.value"
        @click="emit('update:status', option.value)"
      >
        {{ option.label }}
      </button>
    </div>
    <input v-model="search" type="search" class="search" placeholder="Search tasks…" aria-label="Search tasks" />
  </div>
</template>
