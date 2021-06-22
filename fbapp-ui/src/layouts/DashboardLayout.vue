<template>
    <q-layout view="hHh Lpr lFf">
        <SiteToolbar />

        <q-drawer
            side="left"
            :model-value="true"
        >
            <q-list
                no-border
                link
                inset-delimiter
            >
                <q-item>
                    <q-item-label header>
                        Kontrollpaneel
                    </q-item-label>
                </q-item>
                <q-item to="/dashboard/competitions">
                    <q-item-section side>
                        <q-icon name="mdi-trophy" />
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>Võistlused</q-item-label>
                    </q-item-section>
                </q-item>
                <q-item to="/dashboard/leagues">
                    <q-item-section side>
                        <q-icon name="mdi-view-list" />
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>Ennustusliigad</q-item-label>
                    </q-item-section>
                </q-item>
                <q-item>
                    <q-item-section side>
                        <q-icon name="mdi-account-multiple" />
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>Meeskonnad</q-item-label>
                    </q-item-section>
                </q-item>
                <q-item>
                    <q-item-section side>
                        <q-icon name="mdi-soccer-field" />
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>Mängud</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>
        </q-drawer>

        <q-page-container>
            <router-view />
        </q-page-container>
    </q-layout>
</template>

<script>
import DashboardLayout from './DashboardLayout.vue.ts'
export default DashboardLayout
</script>
