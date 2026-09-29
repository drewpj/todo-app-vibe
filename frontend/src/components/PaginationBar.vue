<script setup lang="ts">
defineProps<{ page: number; totalPages: number }>()
const emit = defineEmits<{ change: [page: number] }>()
</script>

<template>
  <nav v-if="totalPages > 1" class="pagination" aria-label="Pagination">
    <button type="button" class="btn btn-ghost" :disabled="page <= 1" @click="emit('change', page - 1)">
      ← Previous
    </button>
    <span>Page {{ page }} of {{ totalPages }}</span>
    <button type="button" class="btn btn-ghost" :disabled="page >= totalPages" @click="emit('change', page + 1)">
      Next →
    </button>
  </nav>
</template>
