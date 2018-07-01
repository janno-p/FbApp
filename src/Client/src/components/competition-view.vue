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
                                    <q-item-tile class="text-center q-py-lg">
                                        <h3 class="q-my-none q-mb-sm">{{ goals(homeGoals) }} : {{ goals(awayGoals) }}</h3>
                                        <p v-if="this.fixture.penalties" class="q-body-2 text-faded">( {{ this.fixture.penalties[0] }} : {{ this.fixture.penalties[1] }} )</p>
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
                            <template v-if="fixture.resultPredictions.length > 0">
                                <q-item v-for="(prediction, j) in fixture.resultPredictions" :key="j">
                                    <q-item-side v-if="isPreFixture" icon="remove" class="q-px-md" />
                                    <q-item-side v-else-if="isCorrectResultPrediction(prediction)" icon="done" color="positive" class="q-px-md" />
                                    <q-item-side v-else icon="close" color="negative" class="q-px-md" />
                                    <q-item-main>
                                        <q-item-tile>{{ prediction.name }}</q-item-tile>
                                    </q-item-main>
                                    <q-item-side class="q-px-md">
                                        <q-item-tile>{{ predictionText(prediction) }}</q-item-tile>
                                    </q-item-side>
                                </q-item>
                            </template>
                            <template v-if="fixture.qualifierPredictions.length > 0">
                                <q-item v-for="(prediction, j) in fixture.qualifierPredictions" :key="j">
                                    <q-item-side :icon="homeQualifiesIcon(prediction)" class="q-px-md" :color="homeQualifiesResultClass(prediction)" />
                                    <q-item-main>
                                        <q-item-tile class="text-center">{{ prediction.name }}</q-item-tile>
                                    </q-item-main>
                                    <q-item-side :icon="awayQualifiesIcon(prediction)" class="q-px-md" :color="awayQualifiesResultClass(prediction)" />
                                </q-item>
                            </template>
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
        homeGoals () {
            return this.fixture.score ? this.fixture.score[0] : null
        },

        awayGoals () {
            return this.fixture.score ? this.fixture.score[1] : null
        },

        fixtureStatus () {
            if (this.fixture.score === null) {
                return "None"
            } else if (this.isHomeWin) {
                return "HomeWin"
            } else if (this.isAwayWin) {
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
            return this.fixture.score === null
        },

        isHomeWin () {
            if (this.fixture.score === null) {
                return false
            }
            const hg = this.fixture.score[0] + (this.fixture.penalties ? this.fixture.penalties[0] : 0)
            const ag = this.fixture.score[1] + (this.fixture.penalties ? this.fixture.penalties[1] : 0)
            return hg > ag
        },

        isAwayWin () {
            if (this.fixture.score === null) {
                return false
            }
            const hg = this.fixture.score[0] + (this.fixture.penalties ? this.fixture.penalties[0] : 0)
            const ag = this.fixture.score[1] + (this.fixture.penalties ? this.fixture.penalties[1] : 0)
            return hg < ag
        },

        isDraw () {
            if (this.fixture.score === null) {
                return false
            }
            const hg = this.fixture.score[0] + (this.fixture.penalties ? this.fixture.penalties[0] : 0)
            const ag = this.fixture.score[1] + (this.fixture.penalties ? this.fixture.penalties[1] : 0)
            return hg === ag
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
                this.fixture.score = response.data.score
                this.fixture.penalties = response.data.penalties
            }
        },

        runUpdate () {
            setTimeout(async () => {
                if (!this.isDestroyed) {
                    try {
                        await this.updateFixture()
                    } finally {
                        this.runUpdate()
                    }
                }
            }, 30000)
        },

        formatDate (d) {
            return moment(d).format("DD.MM.YYYY HH:mm")
        },

        isCorrectResultPrediction (prediction) {
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
        },

        awayQualifiesIcon (prediction) {
            return prediction.awayQualifies ? "done" : "close"
        },

        homeQualifiesIcon (prediction) {
            return prediction.homeQualifies ? "done" : "close"
        },

        awayQualifiesResultClass (prediction) {
            if (this.isPreFixture) {
                return undefined
            } else if (this.isDraw) {
                return "warning"
            } else if (this.isHomeWin) {
                return !prediction.awayQualifies ? "positive" : "negative"
            } else {
                return prediction.awayQualifies ? "positive" : "negative"
            }
        },

        homeQualifiesResultClass (prediction) {
            if (this.isPreFixture) {
                return undefined
            } else if (this.isDraw) {
                return "warning"
            } else if (this.isAwayWin) {
                return !prediction.homeQualifies ? "positive" : "negative"
            } else {
                return prediction.homeQualifies ? "positive" : "negative"
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
