<template>
    <q-header>
        <q-toolbar
            class="glossy"
            color="primary"
            :inverted="false"
        >
            <router-link
                class="text-decoration-none"
                to="/"
            >
                <q-icon
                    class="text-white"
                    name="mdi-soccer"
                    size="24pt"
                />
            </router-link>

            <q-toolbar-title>
                Ennustusmäng
                <template #subtitle>
                    <div>2018. aasta jalgpalli maailmameistrivõistlused</div>
                </template>
            </q-toolbar-title>

            <q-btn
                class="q-mr-sm"
                icon="mdi-playlist-check"
                flat
                dense
                round
                title="Muudatuste logi"
                to="/changelog"
            />

            <template v-if="isGoogleReady">
                <template v-if="isSignedIn">
                    <img :src="sizedImageUrl">
                    <span class="q-pl-sm q-pr-sm text-weight-medium">{{ name }}</span>
                    <q-btn
                        v-if="hasDashboard"
                        flat
                        dense
                        round
                        title="Ava kontrollpaneel"
                        @click="openDashboard"
                    >
                        <q-icon name="mdi-settings" />
                    </q-btn>
                    <q-btn
                        flat
                        dense
                        round
                        title="Logi välja"
                        @click="signOut"
                    >
                        <q-icon name="mdi-logout" />
                    </q-btn>
                </template>
                <q-btn
                    v-else
                    flat
                    dense
                    round
                    title="Logi sisse Google kontoga"
                    @click="signIn"
                >
                    <q-icon name="mdi-google" />
                </q-btn>
            </template>
        </q-toolbar>
    </q-header>
</template>

<script>
import SiteToolbar from './SiteToolbar.vue.ts'
export default SiteToolbar
</script>

<style>
.text-decoration-none {
    text-decoration: none !important;
}
</style>
