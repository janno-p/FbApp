<template>
    <q-page class="q-pa-lg">
        <q-list class="q-mb-sm" v-for="entry in log" :key="entry.version">
            <q-list-header>Versioon {{ entry.version }}</q-list-header>
            <q-item v-for="(change, i) in entry.changes" :key="i">
                <q-item-side>
                    <q-item-tile icon="check" />
                </q-item-side>
                <q-item-main :label="change" />
            </q-item>
        </q-list>
    </q-page>
</template>

<script>
export default {
    name: "PageChangelog",

    data () {
        return {
            log: [
                {
                    version: "0.0.2",
                    changes: [
                        "Enda tehtud ennustuste vaade",
                        "Peale ennustuse registreerimisel oma ennustuse vaate avamine",
                        "Olemasoleva ennustuse korral oma ennustuse vaate avamine"
                    ]
                },
                {
                    version: "0.0.1",
                    changes: [
                        "Ennustuste registreerimine"
                    ]
                }
            ]
        }
    }
}
</script>
