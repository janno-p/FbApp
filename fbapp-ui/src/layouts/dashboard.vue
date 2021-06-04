<template>
    <q-layout view="hHh Lpr lFf">
        <app-site-toolbar />

        <q-layout-drawer side="left" :value="true">
            <q-list no-border link inset-delimiter>
                <q-list-header>Kontrollpaneel</q-list-header>
                <q-item to="/dashboard/competitions">
                    <q-item-side icon="mdi-trophy" />
                    <q-item-main label="Võistlused" />
                </q-item>
                <q-item to="/dashboard/leagues">
                    <q-item-side icon="mdi-view-list" />
                    <q-item-main label="Ennustusliigad" />
                </q-item>
                <q-item>
                    <q-item-side icon="mdi-account-multiple" />
                    <q-item-main label="Meeskonnad" />
                </q-item>
                <q-item>
                    <q-item-side icon="mdi-soccer-field" />
                    <q-item-main label="Mängud" />
                </q-item>
            </q-list>
        </q-layout-drawer>

        <q-page-container>
            <router-view />
        </q-page-container>
    </q-layout>
</template>

<script>
import AppSiteToolbar from '../components/site-toolbar'
import store from '../store/index'

export default {
    name: 'LayoutDashboard',

    components: {
        AppSiteToolbar
    },

    beforeRouteEnter: (to, from, next) => {
        if (store.getters.hasDashboard) {
            next()
        } else {
            next('/')
        }
    }
}
</script>
