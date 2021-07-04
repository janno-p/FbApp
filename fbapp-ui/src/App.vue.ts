import { computed, defineComponent } from 'vue'
import { useAuth } from './boot/auth'

export default defineComponent({
    name: 'App',

    setup () {
        const { state } = useAuth()
        const isReady = computed(() => !!state.value)
        return { isReady }
    }
})
