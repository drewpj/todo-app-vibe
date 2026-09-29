<script setup lang="ts">
import { useId } from 'vue'

defineProps<{
  label: string
  /** Keeps the label available to screen readers while hiding it visually. */
  hideLabel?: boolean
  type?: string
  autocomplete?: string
  error?: string | null
  maxlength?: number
  min?: string
  hint?: string
  multiline?: boolean
  rows?: number
}>()

const model = defineModel<string>({ required: true })
const id = useId()
</script>

<template>
  <div class="field">
    <label :for="id" :class="{ 'sr-only': hideLabel }">{{ label }}</label>
    <textarea
      v-if="multiline"
      :id="id"
      v-model="model"
      :rows="rows ?? 3"
      :maxlength="maxlength"
      :aria-invalid="!!error"
      :aria-describedby="error ? `${id}-error` : undefined"
    />
    <input
      v-else
      :id="id"
      v-model="model"
      :type="type ?? 'text'"
      :autocomplete="autocomplete"
      :maxlength="maxlength"
      :min="min"
      :aria-invalid="!!error"
      :aria-describedby="error ? `${id}-error` : undefined"
    />
    <p v-if="hint && !error" class="field-hint">{{ hint }}</p>
    <p v-if="error" :id="`${id}-error`" class="field-error">{{ error }}</p>
  </div>
</template>
