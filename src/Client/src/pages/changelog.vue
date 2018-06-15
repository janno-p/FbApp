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
                    version: "0.0.3",
                    changes: [
                        "Parandatud iluviga Chromes",
                        "Võistluste alguskuupäeva registreerimine",
                        "Mängutulemuste kontrollimine",
                        "Võistluste alguskuupäeva kontrollimine ennustuste registreerimisel"
                    ]
                },
                {
                    version: "0.0.2",
                    changes: [
                        "Oma ennustuste vaade",
                        "Peale ennustuste registreerimist oma ennustuste vaate avamine",
                        "Olemasoleva ennustuste korral oma ennustuste vaate avamine",
                        "Juhusliku valiku tegemise võimalus"
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
