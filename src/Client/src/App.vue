<template>
    <div id="q-app">
        <router-view />
    </div>
</template>

<script>
import { mapActions } from "vuex"

export default {
    name: "App",

    created () {
        return this.authenticate()
    },

    methods: {
        ...mapActions([
            "authenticate"
        ])
    }
}
</script>
