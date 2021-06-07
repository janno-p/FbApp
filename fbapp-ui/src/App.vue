<template>
    <div id="q-app">
        <router-view />
    </div>
</template>

<script>
import { mapActions } from 'vuex'

export default {
    name: 'App',

    async created () {
        this.$router.push('/')
    },

    methods: {
        ...mapActions([
            'authenticate2'
        ])
    }
}
</script>
