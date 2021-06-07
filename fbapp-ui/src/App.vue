<template>
    <div id="q-app">
        <router-view />
    </div>
</template>

<script>
import { mapActions } from 'vuex'
import authService from './services/auth-service'

export default {
    name: 'App',

    async created () {
        await authService.renewToken()
        // await this.authenticate2()
        this.$router.push('/')
    },

    methods: {
        ...mapActions([
            'authenticate2'
        ])
    }
}
</script>
