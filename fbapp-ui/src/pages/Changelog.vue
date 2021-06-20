<template>
    <q-page class="q-pa-lg">
        <q-list
            v-for="entry in log"
            :key="entry.version"
            class="q-mb-sm"
        >
            <q-list-header>Versioon {{ entry.version }}</q-list-header>
            <q-item
                v-for="(change, i) in entry.changes"
                :key="i"
            >
                <q-item-side>
                    <q-item-tile icon="check" />
                </q-item-side>
                <q-item-main :label="change" />
            </q-item>
        </q-list>
    </q-page>
</template>

<script>
import Changelog from './Changelog.vue.ts'
export default Changelog
</script>
