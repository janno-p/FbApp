<template>
    <q-page>
        <q-tabs color="secondary" inverted>
            <q-route-tab icon="mdi-soccer-field" to="/competition/fixtures" slot="title" title="Mängude tulemused" />
            <q-route-tab icon="list_alt" to="/competition/score-table" slot="title" title="Ennustuste punktitabel" />
        </q-tabs>
        <router-view />
    </q-page>
</template>
