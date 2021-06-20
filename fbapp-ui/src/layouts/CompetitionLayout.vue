<template>
    <q-page>
        <q-tabs
            color="secondary"
            inverted
        >
            <template #title>
                <q-route-tab
                    icon="mdi-soccer-field"
                    to="/competition/fixtures"
                    title="Mängude tulemused"
                />
                <q-route-tab
                    icon="list_alt"
                    to="/competition/score-table"
                    title="Ennustuste punktitabel"
                />
            </template>
        </q-tabs>
        <router-view />
    </q-page>
</template>
