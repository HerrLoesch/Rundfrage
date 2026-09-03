import { createApp } from 'vue'
import { createPinia } from 'pinia'
import 'vuetify/styles'
import { createVuetify } from 'vuetify'
import * as components from 'vuetify/components'
import * as directives from 'vuetify/directives'

import App from './App.vue'
import { i18n } from './i18n'
import { router } from './router'

const vuetify = createVuetify({ components, directives })

createApp(App).use(createPinia()).use(router).use(i18n).use(vuetify).mount('#app')
