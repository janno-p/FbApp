<template>
    <q-layout view="hHh Lpr lFf">
        <SiteToolbar />

        <q-page-container>
            <router-view v-slot="{ Component }">
                <transition
                    appear
                    enter-active-class="animated fadeIn"
                    leave-active-class="animated fadeOut"
                    mode="out-in"
                >
                    <component
                        :is="Component"
                        :key="Component.type.name"
                    />
                </transition>
            </router-view>
        </q-page-container>
    </q-layout>
</template>

<script>
import MainLayout from './MainLayout.vue.ts'
export default MainLayout
</script>
