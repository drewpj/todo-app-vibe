import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import { configureHttp } from './api/http'
import { createAppRouter } from './router'
import { useAuthStore } from './stores/auth'
import { useTodosStore } from './stores/todos'
import './styles.css'

const app = createApp(App)
const pinia = createPinia()
app.use(pinia)

const router = createAppRouter()
app.use(router)

// Any authenticated request that comes back 401 (expired/revoked token) drops the session and returns to login.
const auth = useAuthStore(pinia)
const todos = useTodosStore(pinia)
configureHttp({
  getToken: () => auth.token,
  onUnauthorized: () => {
    auth.logout()
    todos.reset()
    void router.push({ name: 'login' })
  },
})

app.mount('#app')
