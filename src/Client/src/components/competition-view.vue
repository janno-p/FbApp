<template>
    <q-page class="q-pa-lg">
        <q-inner-loading v-if="isInitializing" :visible="true">
            <q-spinner-puff size="100px" color="primary" />
            <p class="q-mt-lg">Võistluste hetkeseisu küsimine &hellip;</p>
        </q-inner-loading>
        <template v-else>
            <div class="row">
                <div class="col-12 col-md-6">
                    <q-list class="q-mx-sm">
                        <q-item>
                            <q-item-side>
                                <q-item-tile>
                                    <q-btn round icon="arrow_back" title="Eelmine mäng" @click="openPrevious" :disabled="!fixture.previousFixtureId" />
                                </q-item-tile>
                            </q-item-side>
                            <q-item-main>
                                <q-item-tile class="text-center">
                                    <div class="q-subtitle text-faded">{{ fixtureTitle }}</div>
                                </q-item-tile>
                            </q-item-main>
                            <q-item-side>
                                <q-item-tile>
                                    <q-btn round icon="arrow_forward" title="Järgmine mäng" @click="openNext" :disabled="!fixture.nextFixtureId" />
                                </q-item-tile>
                            </q-item-side>
                        </q-item>
                        <q-item-separator />
                        <q-item v-if="isLoadingFixture" key="loading">
                            <q-item-main>
                                <q-item-tile>
                                    <q-inner-loading :visible="true">
                                        <q-spinner-puff size="100px" color="primary" />
                                        <p class="q-mt-lg">Mängu andmete laadimine &hellip;</p>
                                    </q-inner-loading>
                                </q-item-tile>
                            </q-item-main>
                        </q-item>
                        <template v-else>
                            <q-item key="fixture">
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
                                <q-item-side v-if="isPreFixture" icon="remove" class="q-px-md" />
                                <q-item-side v-else-if="isCorrectPrediction(prediction)" icon="done" color="positive" class="q-px-md" />
                                <q-item-side v-else icon="close" color="negative" class="q-px-md" />
                                <q-item-main>
                                    <q-item-tile>{{ prediction.name }}</q-item-tile>
                                </q-item-main>
                                <q-item-side class="q-px-md">
                                    <q-item-tile>{{ predictionText(prediction) }}</q-item-tile>
                                </q-item-side>
                            </q-item>
                        </template>
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

    computed: {
        fixtureStatus () {
            if (this.fixture.homeGoals === null || this.fixture.awayGoals === null) {
                return "None"
            } else if (this.fixture.homeGoals > this.fixture.awayGoals) {
                return "HomeWin"
            } else if (this.fixture.homeGoals < this.fixture.awayGoals) {
                return "AwayWin"
            } else {
                return "Tie"
            }
        },

        fixtureTitle () {
            switch (this.fixture.status) {
            case "IN_PLAY":
                return "Käimasolev mäng"
            case "FINISHED":
                return "Lõppenud mäng"
            default:
                return "Toimumata mäng"
            }
        },

        isPreFixture () {
            return this.fixture.homeGoals === null || this.fixture.awayGoals === null
        }
    },

    data () {
        return {
            isDestroyed: false,
            isInitializing: true,
            fixture: null,
            isLoadingFixture: false
        }
    },

    methods: {
        goals (value) {
            return value === null ? "-" : value
        },

        async updateFixture () {
            const response = await this.$axios.get(`/fixtures/${this.fixture.id}/status`)
            if (response.data) {
                this.fixture.status = response.data.status
                this.fixture.homeGoals = response.data.homeGoals
                this.fixture.awayGoals = response.data.awayGoals
            }
        },

        runUpdate () {
            setTimeout(async () => {
                try {
                    await this.updateFixture()
                } finally {
                    if (!this.isDestroyed) {
                        this.runUpdate()
                    }
                }
            }, 30000)
        },

        formatDate (d) {
            return moment(d).format("DD.MM.YYYY HH:mm")
        },

        isCorrectPrediction (prediction) {
            return this.fixtureStatus === prediction.result
        },

        predictionText (prediction) {
            switch (prediction.result) {
            case "HomeWin":
                return this.fixture.homeTeam.name
            case "AwayWin":
                return this.fixture.awayTeam.name
            case "Tie":
                return "Draw"
            }
        },

        async loadFixture (id) {
            try {
                this.isLoadingFixture = true
                const response = await this.$axios.get(`/fixtures/${id}`)
                this.$set(this, "fixture", response.data)
            } finally {
                this.isLoadingFixture = false
            }
        },

        async openPrevious () {
            if (this.fixture.previousFixtureId) {
                await this.loadFixture(this.fixture.previousFixtureId)
            }
        },

        async openNext () {
            if (this.fixture.nextFixtureId) {
                await this.loadFixture(this.fixture.nextFixtureId)
            }
        },

        handleKeyboardInput (event) {
            switch (event.which) {
            case 37:
                this.openPrevious()
                break
            case 39:
                this.openNext()
                break
            }
        }
    },

    created () {
        window.addEventListener("keyup", this.handleKeyboardInput)
    },

    mounted () {
        this.$nextTick(async () => {
            try {
                const response = await this.$axios.get("/fixtures/timely")
                this.$set(this, "fixture", response.data)
            } finally {
                this.runUpdate()
                this.isInitializing = false
            }
        })
    },

    beforeDestroy () {
        window.removeEventListener("keyup", this.handleKeyboardInput)
        this.isDestroyed = true
    }
}
</script>
