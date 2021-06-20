<template>
    <q-page>
        <q-tabs
            class="text-cyan"
            align="left"
            inverted
        >
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
        </q-tabs>
        <router-view />
    </q-page>
</template>
