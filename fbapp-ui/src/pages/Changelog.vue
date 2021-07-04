<template>
    <q-page class="q-pa-lg">
        <q-list v-for="entry in log" :key="entry.version" class="q-mb-sm">
            <q-item-label header>Versioon {{ entry.version }}</q-item-label>
            <q-item v-for="(change, i) in entry.changes" :key="i">
                <q-item-section side>
                    <q-icon name="mdi-check" />
                </q-item-section>
                <q-item-section>{{ change }}</q-item-section>
            </q-item>
        </q-list>
    </q-page>
</template>

<script>
import Changelog from './Changelog.vue.ts'
export default Changelog
</script>
