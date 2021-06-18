<template>
    <div id="q-app">
        <router-view />
    </div>
</template>

<script>
import { mapActions } from "vuex"

export default {
    name: "App",

    async created () {
        await this.authenticate()
        this.$router.push("/")
    },

    methods: {
        ...mapActions([
            "authenticate"
        ])
    }
}
</script>
