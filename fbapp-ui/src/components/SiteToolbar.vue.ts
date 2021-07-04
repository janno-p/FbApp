import { useAuth } from 'src/boot/auth'
import useActiveCompetition from 'src/use/active-competition'
import { computed, defineComponent } from 'vue'
import { useRouter } from 'vue-router'

export default defineComponent({
    name: 'SiteToolbar',

    setup () {
        const router = useRouter()
        const { isLoading, competition } = useActiveCompetition()
        const { logout, state } = useAuth()

        function goHome () {
            return router.push({ name: 'home' })
        }

        const competitionName = computed(() => {
            return isLoading.value ? '' : competition.value?.name ?? ''
        })

        const isAnonymousUser = computed(() => state.value?.kind === 'anonymous')

        const avatarIcon = computed(() => 'mdi-account-tie')

        const authenticatedUser = computed(() => state.value?.kind === 'authenticated' ? state.value.user : null)
        const userName = computed(() => authenticatedUser.value?.profile.name)

        return {
            avatarIcon,
            competitionName,
            goHome,
            isAnonymousUser,
            logout,
            userName
        }
    }
})
