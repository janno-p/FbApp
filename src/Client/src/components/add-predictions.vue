<template>
    <q-page class="q-pa-lg">
        <q-stepper ref="stepper" vertical no-header-navigation v-model="currentStep">
            <q-step :name="0" default title="Ennustusmäng" subtitle="Veel on aega">
                <p>Ajavahemikus 14. juunist 15. juulini toimuvad Venemaal 2018. aasta jalgpalli
                    maailmameistrivõistlused. Lisaks rahvusmeeskondade mõõduvõtmistele pakub antud veebileht
                    omavahelist võistlusmomenti ka tugitoolisportlastele tulemuste ennustamise näol.</p>

                <p>Oma eelistusi saad valida ja muuta kuni avamänguni 14. juunil kell 18:00. Pärast seda on
                    võimalik sama veebilehe vahendusel jälgida, kuidas tegelikud tulemused kujunevad ning kui täpselt
                    need Sinu või teiste ennustustega kokku langevad.</p>

                <p>Auhinnaks lühiajaline au ja kuulsus.</p>

                <q-btn color="positive" @click="moveToGroupStage" label="Tee oma ennustused »" />
            </q-step>

            <q-step :name="1" title="Alagrupimängud" subtitle="Kes võidab mängu?">
                <template v-if="!!fixtures">
                    <div class="row q-pa-md">
                        <div v-for="(f, i) in fixtures" :key="i" class="q-py-md col-2 col-lg-3 col-md-4 col-sm-6 col-xs-12">
                            <q-btn :color="f.result === 'HOME' ? 'positive' : undefined" round glossy :title="f.homeTeam.name" @click="f.result = 'HOME'"><img :src="f.homeTeam.flagUrl" height="16" /></q-btn>
                            &nbsp;
                            <q-btn :color="f.result === 'TIE' ? 'positive' : undefined" round title="Jääb viiki" @click="f.result = 'TIE'">=</q-btn>
                            &nbsp;
                            <q-btn :color="f.result === 'AWAY' ? 'positive' : undefined" round glossy :title="f.awayTeam.name" @click="f.result = 'AWAY'"><img :src="f.awayTeam.flagUrl" height="16" /></q-btn>
                        </div>
                    </div>

                    <q-btn color="primary" @click="moveToNextQualRound" label="Jätka alagrupist edasipääsejate ennustamisega »" :disabled="!groupStageComplete" />
                </template>
                <p v-else>Toimuvate mängude laadimine, oota natuke &hellip;</p>
            </q-step>

            <template v-for="(c, i) in steps">
                <q-step v-if="!!c" :key="i" :name="i" :title="c.title" :subtitle="c.subtitle">
                    <template v-if="currentStep === i">
                        <div class="row q-pb-md">
                            <div v-for="(g, j) in qualifiers[i].teams" :key="j" class="q-pa-xs col-2 col-lg-3 col-md-4 col-sm-6 col-xs-12">
                                <q-list>
                                    <q-list-header>{{ j.toUpperCase() }} alagrupp</q-list-header>
                                    <q-item v-for="(x, k) in g" :key="k">
                                        <q-item-side>
                                            <q-btn :color="getColor(x)" :disabled="isDisabled(x)" round glossy title="Vali võistkond" @click="changeSelection(x)"><img :src="x.team.flagUrl" height="16" /></q-btn>
                                        </q-item-side>
                                        <q-item-main>
                                            <q-item-tile>{{ x.team.name }}</q-item-tile>
                                        </q-item-main>
                                    </q-item>
                                </q-list>
                            </div>
                        </div>

                        <q-btn v-if="currentStep < 6" color="primary" @click="moveToNextQualRound" :label="c.buttonText" :disabled="!qualifiers[i].isFull" />
                        <q-btn v-else-if="isSignedIn" color="positive" label="Registreeri oma ennustus" @click="registerPrediction" :disabled="!qualifiers[i].isFull" :loading="isSaveInProgress">
                            <q-spinner-pie slot="loading" />
                        </q-btn>
                        <q-btn v-else color="positive" icon="mdi-google" label="Registreeri oma ennustus Google kontoga" @click="registerPrediction" :disabled="!qualifiers[i].isFull" :loading="isSaveInProgress">
                            <q-spinner-pie slot="loading" />
                        </q-btn>
                    </template>
                </q-step>
            </template>

            <q-inner-loading :visible="isLoadingStep">
                <q-spinner-puff size="50px" color="primary"></q-spinner-puff>
            </q-inner-loading>
        </q-stepper>

        <q-page-sticky position="bottom" :offset="[18, 18]" v-if="displayCounter">
            <q-rating readonly :max="counterValue" :value="counterValue" color="positive" />
        </q-page-sticky>
    </q-page>
</template>

<script>
import _ from "lodash"
import { Notify } from "quasar"
import { mapActions, mapState } from "vuex"

class SelectedTeam {
    constructor (team, qual, cb) {
        this.selected = false
        this.team = team
        this.qual = qual
        this.cb = cb
    }

    setSelected (value) {
        this.selected = value
        this.cb(this)
    }
}

class QualifierList {
    constructor (teams, count) {
        this.count = count
        this.selectedCount = 0
        this.teams = _(teams).mapValues((x) => _(x).map((t) => new SelectedTeam(t, this, (u) => this.updateCount(u))).value()).value()
    }

    get remainingCount () {
        return this.count - this.selectedCount
    }

    get isFull () {
        return this.remainingCount < 1
    }

    updateCount (team) {
        this.selectedCount += team.selected ? 1 : -1
    }
}

function mapResult (r) {
    switch (r) {
    case "HOME": return "HomeWin"
    case "AWAY": return "AwayWin"
    case "TIE": return "Tie"
    default: return null
    }
}

export default {
    name: "AppAddPredictions",

    computed: {
        groupStageComplete () {
            return !!this.fixtures && _(this.fixtures).every((f) => !!f.result)
        },

        counterValue () {
            return this.displayCounter ? this.qualifiers[this.currentStep].remainingCount : 0
        },

        displayCounter () {
            return this.currentStep >= 2 && this.currentStep <= 6
        },

        ...mapState([
            "isSignedIn"
        ])
    },

    data () {
        return {
            teams: null,
            isSaveInProgress: false,
            competitionId: null,
            currentStep: 0,
            isLoadingStep: false,
            fixtures: null,
            qualifiers: null,
            steps: [
                null,
                null,
                {
                    title: "Alagrupist edasipääsejad",
                    subtitle: "Millised meeskonnad jätkavad väljalangemismängudega?",
                    buttonText: "Jätka veerandfinalistide ennustamisega »"
                },
                {
                    title: "Veerandfinalistid",
                    subtitle: "Millised meeskonnad jõuavad veerandfinaalidesse?",
                    buttonText: "Jätka poolfinalistide ennustamisega »"
                },
                {
                    title: "Poolfinalistid",
                    subtitle: "Millised meeskonnad jõuavad poolfinaalidesse?",
                    buttonText: "Jätka finalistide ennustamisega »"
                },
                {
                    title: "Finalistid",
                    subtitle: "Millised on kaks meeskonda, kelle vahel selgitatakse turniiri võitja?",
                    buttonText: "Jätka võitja ennustamisega »"
                },
                {
                    title: "Maailmameister",
                    subtitle: "Milline meeskond on uus maailmameister?",
                    buttonText: null
                }
            ]
        }
    },

    methods: {
        async moveToGroupStage () {
            this.isLoadingStep = true
            const response = await this.$axios.get("/predict/fixtures")
            const teams = _(response.data.teams).mapValues((t, k) => ({ id: k, ...t })).value()
            this.$set(this, "teams", teams)
            this.competitionId = response.data.competitionId
            const fixtures = _(response.data.fixtures)
                .map((f) => ({
                    id: f.id,
                    homeTeam: teams[f.homeTeamId],
                    awayTeam: teams[f.awayTeamId],
                    result: null
                }))
                .filter((f) => !!f.homeTeam && !!f.awayTeam)
                .value()
            this.$set(this, "fixtures", fixtures)
            const groups = _(response.data.groups)
                .mapValues((x) => _(x).map((t) => teams[t]).value())
                .value()
            const qualifiers = [
                null,
                null,
                new QualifierList(groups, 16),
                new QualifierList(groups, 8),
                new QualifierList(groups, 4),
                new QualifierList(groups, 2),
                new QualifierList(groups, 1)
            ]
            this.$set(this, "qualifiers", qualifiers)
            this.isLoadingStep = false
            this.$refs.stepper.next()
        },

        moveToNextQualRound () {
            this.$refs.stepper.next()
        },

        changeSelection (item) {
            if (!item.qual.isFull || item.selected) {
                item.setSelected(!item.selected)
            }
        },

        isDisabled (item) {
            return !item.selected && item.qual.isFull
        },

        getColor (item) {
            if (item.selected) {
                return "positive"
            } else if (item.qual.isFull) {
                return "negative"
            } else {
                return undefined
            }
        },

        async registerPrediction () {
            this.isSaveInProgress = true
            try {
                if (!this.isSignedIn) {
                    await this.googleSignIn()
                }
                const mapQualifiers = (i) => _(this.qualifiers[i].teams).values().flatten().filter((x) => x.selected).map((x) => x.team.id).value()
                const payload = {
                    competitionId: this.competitionId,
                    fixtures: _(this.fixtures).map((x) => ({ id: x.id, result: x.result })).value(),
                    qualifiers: {
                        roundOf16: mapQualifiers(2),
                        roundOf8: mapQualifiers(3),
                        roundOf4: mapQualifiers(4),
                        roundOf2: mapQualifiers(5)
                    },
                    winner: mapQualifiers(6)[0]
                }
                await this.$axios.post("/predict/", payload)
                const predictions = {
                    competitionId: payload.competitionId,
                    teams: this.teams,
                    fixtures: _(this.fixtures).map((f) => ({
                        fixture: f.id,
                        homeTeam: f.homeTeam.id,
                        awayTeam: f.awayTeam.id,
                        result: mapResult(f.result)
                    })).value(),
                    roundOf16: payload.qualifiers.roundOf16,
                    roundOf8: payload.qualifiers.roundOf8,
                    roundOf4: payload.qualifiers.roundOf4,
                    roundOf2: payload.qualifiers.roundOf2,
                    winner: payload.winner
                }
                this.$emit("prediction-added", predictions)
            } catch (error) {
                if (error.response && error.response.status === 409) {
                    Notify.create({
                        message: "Oled juba varasemalt oma ennustuse teinud!",
                        position: "bottom",
                        type: "warning",
                        actions: [
                            {
                                label: "Sulge"
                            }
                        ]
                    })
                    this.$emit("prediction-exists")
                    return
                }
                throw error
            } finally {
                this.isSaveInProgress = false
            }
        },

        ...mapActions([
            "googleSignIn"
        ])
    }
}
</script>
