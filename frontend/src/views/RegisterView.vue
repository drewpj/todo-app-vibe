<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { ApiError } from '@/api/http'
import FormField from '@/components/FormField.vue'
import { useAuthStore } from '@/stores/auth'

const MIN_PASSWORD_LENGTH = 8
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

const auth = useAuthStore()
const router = useRouter()

const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const errors = ref<{ email?: string; password?: string; confirm?: string }>({})
const formError = ref<string | null>(null)
const submitting = ref(false)

function validate(): boolean {
  errors.value = {}
  if (!EMAIL_PATTERN.test(email.value.trim())) errors.value.email = 'Enter a valid email address.'
  if (password.value.length < MIN_PASSWORD_LENGTH) {
    errors.value.password = `Password must be at least ${MIN_PASSWORD_LENGTH} characters.`
  }
  if (confirmPassword.value !== password.value) errors.value.confirm = 'Passwords do not match.'
  return Object.keys(errors.value).length === 0
}

async function submit() {
  formError.value = null
  if (!validate()) return

  submitting.value = true
  try {
    await auth.register(email.value, password.value)
    await router.replace({ name: 'todos' })
  } catch (e) {
    formError.value = e instanceof ApiError ? e.message : 'Could not create the account. Please try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="auth-card">
    <h1>Create your account</h1>
    <form novalidate @submit.prevent="submit">
      <FormField v-model="email" label="Email" type="email" autocomplete="email" :error="errors.email" />
      <FormField
        v-model="password"
        label="Password"
        type="password"
        autocomplete="new-password"
        :error="errors.password"
        :hint="`At least ${MIN_PASSWORD_LENGTH} characters`"
      />
      <FormField
        v-model="confirmPassword"
        label="Confirm password"
        type="password"
        autocomplete="new-password"
        :error="errors.confirm"
      />
      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>
      <button type="submit" class="btn btn-primary btn-block" :disabled="submitting">
        {{ submitting ? 'Creating account…' : 'Create account' }}
      </button>
    </form>
    <p class="auth-switch">Already registered? <RouterLink :to="{ name: 'login' }">Sign in</RouterLink></p>
  </section>
</template>
