<template>
    <q-layout view="hHh Lpr lFf">
        <SiteToolbar />

        <transition
            appear
            enter-active-class="animated fadeIn"
            leave-active-class="animated fadeOut"
            mode="out-in"
        >
            <q-drawer
                class="q-pa-md"
                :model-value="true"
                show-if-above
                :width="250"
                :breakpoint="500"
            >
                <q-scroll-area class="fit">
                    <q-list
                        padding
                        class="menu-list"
                    >
                        <q-item
                            v-ripple
                            clickable
                            :to="{ name: 'competitions' }"
                        >
                            <q-item-section avatar>
                                <q-icon name="mdi-trophy" />
                            </q-item-section>

                            <q-item-section>
                                Võistlused
                            </q-item-section>
                        </q-item>

                        <q-item
                            v-ripple
                            active
                            clickable
                        >
                            <q-item-section avatar>
                                <q-icon name="mdi-view-list" />
                            </q-item-section>

                            <q-item-section>
                                Ennustusliigad
                            </q-item-section>
                        </q-item>

                        <q-item
                            v-ripple
                            clickable
                        >
                            <q-item-section avatar>
                                <q-icon name="mdi-account-multiple" />
                            </q-item-section>

                            <q-item-section>
                                Meeskonnad
                            </q-item-section>
                        </q-item>

                        <q-item
                            v-ripple
                            clickable
                        >
                            <q-item-section avatar>
                                <q-icon name="mdi-soccer-field" />
                            </q-item-section>

                            <q-item-section>
                                Mängud
                            </q-item-section>
                        </q-item>
                    </q-list>
                </q-scroll-area>
            </q-drawer>
        </transition>

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
                        key="Component.type.name"
                    />
                </transition>
            </router-view>
        </q-page-container>
    </q-layout>
</template>

<script>
import DashboardLayout from './DashboardLayout.vue.ts'
export default DashboardLayout
</script>
