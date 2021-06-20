<template>
    <q-layout view="hHh Lpr lFf">
        <SiteToolbar />

        <q-page-container>
            <router-view />
        </q-page-container>
    </q-layout>
</template>

<script>
import MainLayout from './MainLayout.vue.ts'
export default MainLayout
</script>
