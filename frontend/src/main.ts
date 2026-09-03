import { createApp } from 'vue'
import { createPinia } from 'pinia'
import 'vuetify/styles'
import '@mdi/font/css/materialdesignicons.css'

import App from './App.vue'
import { i18n } from './i18n'
import { router } from './router'
import { vuetify } from './vuetify'

createApp(App).use(createPinia()).use(router).use(i18n).use(vuetify).mount('#app')
