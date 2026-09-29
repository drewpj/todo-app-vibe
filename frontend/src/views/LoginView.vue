<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '@/api/http'
import FormField from '@/components/FormField.vue'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const email = ref('')
const password = ref('')
const error = ref<string | null>(null)
const submitting = ref(false)

/** Only follow same-site relative redirects (guards against open-redirect via ?redirect=https://evil). */
function safeRedirect(value: unknown): string {
  return typeof value === 'string' && value.startsWith('/') && !value.startsWith('//') ? value : '/'
}

async function submit() {
  error.value = null
  if (!email.value.trim() || !password.value) {
    error.value = 'Enter your email and password.'
    return
  }

  submitting.value = true
  try {
    await auth.login(email.value, password.value)
    await router.replace(safeRedirect(route.query.redirect))
  } catch (e) {
    error.value = e instanceof ApiError ? e.message : 'Could not sign in. Please try again.'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <section class="auth-card">
    <h1>Sign in</h1>
    <form novalidate @submit.prevent="submit">
      <FormField v-model="email" label="Email" type="email" autocomplete="email" />
      <FormField v-model="password" label="Password" type="password" autocomplete="current-password" />
      <p v-if="error" class="form-error" role="alert">{{ error }}</p>
      <button type="submit" class="btn btn-primary btn-block" :disabled="submitting">
        {{ submitting ? 'Signing in…' : 'Sign in' }}
      </button>
    </form>
    <p class="auth-switch">No account yet? <RouterLink :to="{ name: 'register' }">Create one</RouterLink></p>
  </section>
</template>
