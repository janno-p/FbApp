import SiteToolbar from 'components/SiteToolbar.vue'
import useAuthentication from 'src/hooks/authentication'
import { defineComponent } from 'vue'

export default defineComponent({
    name: 'DashboardLayout',

    components: {
        SiteToolbar
    },

    beforeRouteEnter: (_to, _from, next) => {
        const { hasDashboard } = useAuthentication()
        if (hasDashboard.value) {
            return next()
        } else {
            return next('/')
        }
    }
})
