<template>
    <q-page class="q-pa-lg">
        <q-inner-loading v-if="isInitializing" :visible="true">
            <q-spinner-puff size="100px" color="primary" />
            <p class="q-mt-lg">Võistluste hetkeseisu küsimine &hellip;</p>
        </q-inner-loading>
        <template v-else>
            <!--<h4 class="q-mt-none">Aktuaalsed mängud</h4>-->
            <div class="row">
                <div class="col-12 col-md-6" v-for="(fixture, i) in fixtures" :key="i">
                    <q-list class="q-mx-sm">
                        <q-list-header class="text-center">{{ title(fixture) }}</q-list-header>
                        <q-item>
                            <q-item-side>
                                <q-item-tile class="text-center q-pa-lg">
                                    <img :src="fixture.homeTeam.flagUrl" height="32" :title="fixture.homeTeam.name" />
                                </q-item-tile>
                                <q-item-tile class="text-center">{{ fixture.homeTeam.name }}</q-item-tile>
                            </q-item-side>
                            <q-item-main>
                                <q-item-tile class="text-center">
                                    <h3>{{ goals(fixture.homeGoals) }} : {{ goals(fixture.awayGoals) }}</h3>
                                </q-item-tile>
                                <q-item-tile class="text-center">
                                    {{ formatDate(fixture.date) }}
                                </q-item-tile>
                            </q-item-main>
                            <q-item-side>
                                <q-item-tile class="text-center q-pa-lg">
                                    <img :src="fixture.awayTeam.flagUrl" height="32" :title="fixture.awayTeam.name" />
                                </q-item-tile>
                                <q-item-tile class="text-center">{{ fixture.awayTeam.name }}</q-item-tile>
                            </q-item-side>
                        </q-item>
                        <q-item-separator />
                        <q-item>tere</q-item>
                    </q-list>
                </div>
            </div>
        </template>
    </q-page>
</template>

<script>
import moment from "moment"

export default {
    name: "AppCompetitionView",

    data () {
        return {
            isInitializing: true,
            fixtures: []
        }
    },

    methods: {
        title (fixture) {
            switch (fixture.status) {
            case "IN_GAME":
                return "Käimasolev mäng"
            case "FINISHED":
                return "Lõppenud mäng"
            default:
                return "Järgmine mäng"
            }
        },

        goals (value) {
            return value === null ? "-" : value
        },

        async updateFixtures () {
            const response = await this.$axios.get("/predict/timely")
            this.$set(this, "fixtures", response.data)
        },

        runUpdate () {
            setTimeout(async () => {
                try {
                    await this.updateFixtures()
                } finally {
                    this.runUpdate()
                }
            }, 30000)
        },

        formatDate (d) {
            return moment(d).format("DD.MM.YYYY HH:mm")
        }
    },

    mounted () {
        this.$nextTick(async () => {
            try {
                await this.updateFixtures()
            } finally {
                this.runUpdate()
                this.isInitializing = false
            }
        })
    }
}
</script>
