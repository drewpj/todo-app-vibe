<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { useTodosStore } from '@/stores/todos'

const auth = useAuthStore()
const todos = useTodosStore()
const route = useRoute()
const router = useRouter()

const showHeader = computed(() => auth.isAuthenticated && !route.meta.guestOnly)

async function signOut() {
  auth.logout()
  todos.reset()
  await router.push({ name: 'login' })
}
</script>

<template>
  <header v-if="showHeader" class="app-header">
    <div class="container header-inner">
      <span class="brand">✅ Todo</span>
      <div class="header-user">
        <span class="user-email" :title="auth.user?.email">{{ auth.user?.email }}</span>
        <button type="button" class="btn btn-ghost" @click="signOut">Sign out</button>
      </div>
    </div>
  </header>
  <main class="container">
    <RouterView />
  </main>
</template>
