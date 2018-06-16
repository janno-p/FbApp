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
                        <q-item>
                            <q-item-side>
                                <q-item-tile>
                                    <q-btn round icon="arrow_back" title="Eelmine mäng" />
                                </q-item-tile>
                            </q-item-side>
                            <q-item-main>
                                <q-item-tile class="text-center">
                                    <div class="q-subtitle text-faded">{{ title(fixture) }}</div>
                                </q-item-tile>
                            </q-item-main>
                            <q-item-side>
                                <q-item-tile>
                                    <q-btn round icon="arrow_forward" title="Järgmine mäng" />
                                </q-item-tile>
                            </q-item-side>
                        </q-item>
                        <q-item-separator />
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
                                <q-item-tile class="text-center text-faded q-caption">
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
                        <q-item v-for="(prediction, j) in fixture.predictions" :key="j">
                            <q-item-side v-if="isPreFixture(fixture)" icon="remove" class="q-px-md" />
                            <q-item-side v-else-if="isCorrectPrediction(fixture, prediction)" icon="done" color="positive" class="q-px-md" />
                            <q-item-side v-else icon="close" color="negative" class="q-px-md" />
                            <q-item-main>
                                <q-item-tile>{{ prediction.name }}</q-item-tile>
                            </q-item-main>
                            <q-item-side class="q-px-md">
                                <q-item-tile>{{ predictionText(fixture, prediction) }}</q-item-tile>
                            </q-item-side>
                        </q-item>
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
            case "IN_PLAY":
                return "Käimasolev mäng"
            case "FINISHED":
                return "Lõppenud mäng"
            default:
                return "Toimumata mäng"
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
        },

        isPreFixture (fixture) {
            return fixture.homeGoals === null || fixture.awayGoals === null
        },

        getFixtureStatus (fixture) {
            if (fixture.homeGoals === null || fixture.awayGoals === null) {
                return "None"
            } else if (fixture.homeGoals > fixture.awayGoals) {
                return "HomeWin"
            } else if (fixture.homeGoals < fixture.awayGoals) {
                return "AwayWin"
            } else {
                return "Tie"
            }
        },

        isCorrectPrediction (fixture, prediction) {
            return this.getFixtureStatus(fixture) === prediction.result
        },

        predictionText (fixture, prediction) {
            switch (prediction.result) {
            case "HomeWin":
                return fixture.homeTeam.name
            case "AwayWin":
                return fixture.awayTeam.name
            case "Tie":
                return "Draw"
            }
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
