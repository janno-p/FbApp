import useAuthentication from 'src/hooks/authentication'
import { computed, defineComponent } from 'vue'
import { useRouter } from 'vue-router'

export default defineComponent({
    name: 'SiteToolbar',

    setup () {
        const router = useRouter()

        const { googleSignIn, googleSignOut, hasDashboard, isGoogleReady, isSignedIn, imageUrl, name } = useAuthentication()
        const sizedImageUrl = computed(() => `${imageUrl.value}?sz=32`)

        async function signIn () {
            await googleSignIn()
            await router.push('/')
        }

        async function signOut () {
            await googleSignOut()
            await router.push('/')
        }

        function openDashboard () {
            return router.push('/dashboard')
        }

        return {
            hasDashboard,
            isGoogleReady,
            isSignedIn,
            name,
            openDashboard,
            signIn,
            signOut,
            sizedImageUrl
        }
    }
})
