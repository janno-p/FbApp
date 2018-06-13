<template>
    <q-layout-header>
        <q-toolbar color="primary" :glossy="true" :inverted="false">
            <router-link to="/">
                <q-icon class="text-white" name="mdi-soccer" size="24pt" />
            </router-link>

            <q-toolbar-title>
                Ennustusmäng
                <div slot="subtitle">2018. aasta jalgpalli maailmameistrivõistlused</div>
            </q-toolbar-title>

            <q-btn class="q-mr-sm" icon="mdi-playlist-check" flat dense round title="Muudatuste logi" to="/changelog" />

            <template v-if="isGoogleReady">
                <template v-if="isSignedIn">
                    <img :src="sizedImageUrl" />
                    <span class="q-pl-sm q-pr-sm text-weight-medium">{{ name }}</span>
                    <q-btn v-if="hasDashboard" flat dense round title="Ava kontrollpaneel" @click="$router.push('/dashboard')">
                        <q-icon name="mdi-settings" />
                    </q-btn>
                    <q-btn flat dense round @click="signOut" title="Logi välja">
                        <q-icon name="mdi-logout" />
                    </q-btn>
                </template>
                <q-btn v-else flat dense round @click="googleSignIn" title="Logi sisse Google kontoga">
                    <q-icon name="mdi-google" />
                </q-btn>
            </template>
        </q-toolbar>
    </q-layout-header>
</template>

<script>
import { mapState, mapActions, mapGetters } from "vuex"

export default {
    name: "AppSiteToolbar",

    computed: {
        ...mapState([
            "isGoogleReady",
            "isSignedIn",
            "imageUrl",
            "name"
        ]),
        ...mapGetters([
            "hasDashboard"
        ]),
        sizedImageUrl () {
            return `${this.imageUrl}?sz=32`
        }
    },

    methods: {
        ...mapActions([
            "googleSignIn",
            "googleSignOut"
        ]),
        async signOut () {
            await this.googleSignOut()
            this.$router.push("/")
        }
    }
}
</script>
