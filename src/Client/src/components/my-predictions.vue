<template>
    <q-page class="q-pa-lg">
        <h4 class="q-mt-none q-mb-sm">Minu ennustused</h4>
        <q-list>
            <q-list-header>Alagrupimängud</q-list-header>
            <q-item v-for="fixt in fixtures" :key="fixt.fixture">
                <q-item-main>
                    <q-item-tile>
                        <q-btn :color="fixt.result === 'HomeWin' ? 'positive' : undefined" round glossy :title="fixt.homeTeam.name" disabled>
                            <img :src="fixt.homeTeam.flagUrl" height="16" :title="fixt.homeTeam.name" />
                        </q-btn>
                        &nbsp;
                        <q-btn :color="fixt.result === 'Tie' ? 'positive' : undefined" round glossy title="Jääb viiki" disabled>
                            =
                        </q-btn>
                        &nbsp;
                        <q-btn :color="fixt.result === 'AwayWin' ? 'positive' : undefined" round glossy :title="fixt.awayTeam.name" disabled>
                            <img :src="fixt.awayTeam.flagUrl" height="16" :title="fixt.awayTeam.name" />
                        </q-btn>
                    </q-item-tile>
                </q-item-main>
            </q-item>
        </q-list>

        <q-list class="q-mt-sm">
            <q-list-header>Alagruppidest edasipääsejad</q-list-header>
            <q-item v-for="team in roundOf16" :key="team.name">
                <q-item-side>
                    <q-btn round glossy :title="team.name" disabled>
                        <img :src="team.flagUrl" height="16" :title="team.name" />
                    </q-btn>
                </q-item-side>
                <q-item-main>
                    <q-item-tile>{{ team.name }}</q-item-tile>
                </q-item-main>
            </q-item>
        </q-list>

        <q-list class="q-mt-sm">
            <q-list-header>Veerandfinalistid</q-list-header>
            <q-item v-for="team in roundOf8" :key="team.name">
                <q-item-side>
                    <q-btn round glossy :title="team.name" disabled>
                        <img :src="team.flagUrl" height="16" :title="team.name" />
                    </q-btn>
                </q-item-side>
                <q-item-main>
                    <q-item-tile>{{ team.name }}</q-item-tile>
                </q-item-main>
            </q-item>
        </q-list>

        <q-list class="q-mt-sm">
            <q-list-header>Poolfinalistid</q-list-header>
            <q-item v-for="team in roundOf4" :key="team.name">
                <q-item-side>
                    <q-btn round glossy :title="team.name" disabled>
                        <img :src="team.flagUrl" height="16" :title="team.name" />
                    </q-btn>
                </q-item-side>
                <q-item-main>
                    <q-item-tile>{{ team.name }}</q-item-tile>
                </q-item-main>
            </q-item>
        </q-list>

        <q-list class="q-mt-sm">
            <q-list-header>Finalistid</q-list-header>
            <q-item v-for="team in roundOf2" :key="team.name">
                <q-item-side>
                    <q-btn round glossy :title="team.name" disabled>
                        <img :src="team.flagUrl" height="16" :title="team.name" />
                    </q-btn>
                </q-item-side>
                <q-item-main>
                    <q-item-tile>{{ team.name }}</q-item-tile>
                </q-item-main>
            </q-item>
        </q-list>

        <q-list class="q-mt-sm">
            <q-list-header>Turniiri võitja</q-list-header>
            <q-item>
                <q-item-side>
                    <q-btn round glossy :title="winner.name" disabled>
                        <img :src="winner.flagUrl" height="16" :title="winner.name" />
                    </q-btn>
                </q-item-side>
                <q-item-main>
                    <q-item-tile>{{ winner.name }}</q-item-tile>
                </q-item-main>
            </q-item>
        </q-list>
    </q-page>
</template>

<script>
import _ from "lodash"

export default {
    name: "AppMyPredictions",

    computed: {
        fixtures () {
            return _(this.predictions.fixtures).map((f) => ({
                homeTeam: this.predictions.teams[f.homeTeam],
                awayTeam: this.predictions.teams[f.awayTeam],
                fixture: f.fixture,
                result: f.result
            })).value()
        },

        roundOf16 () {
            return _(this.predictions.roundOf16).map((id) => this.predictions.teams[id]).value()
        },

        roundOf8 () {
            return _(this.predictions.roundOf8).map((id) => this.predictions.teams[id]).value()
        },

        roundOf4 () {
            return _(this.predictions.roundOf4).map((id) => this.predictions.teams[id]).value()
        },

        roundOf2 () {
            return _(this.predictions.roundOf2).map((id) => this.predictions.teams[id]).value()
        },

        winner () {
            return this.predictions.teams[this.predictions.winner]
        }
    },

    props: {
        predictions: {
            type: Object,
            required: true
        }
    }
}
</script>
