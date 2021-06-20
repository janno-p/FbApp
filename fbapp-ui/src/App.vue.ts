import { defineComponent } from 'vue'
import { useRouter } from 'vue-router'
import useAuthentication from './hooks/authentication'

export default defineComponent({
    name: 'App',

    setup () {
        const router = useRouter()
        const { authenticate } = useAuthentication()
        void authenticate().then(() => router.push('/'))
    }
})
