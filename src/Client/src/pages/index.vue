<template>
    <q-page class="q-pa-lg">
        <q-stepper ref="stepper" vertical no-header-navigation>
            <q-step default title="Ennustusmäng" subtitle="Veel on aega">
                <p>Ajavahemikus 14. juunist 15. juulini toimuvad Venemaal 2018. aasta jalgpalli
                    maailmameistrivõistlused. Lisaks rahvusmeeskondade mõõduvõtmistele pakub antud veebileht
                    omavahelist võistlusmomenti ka tugitoolisportlastele tulemuste ennustamise näol.</p>

                <p>Oma eelistusi saad valida ja muuta kuni avamänguni 14. juunil kell 18:00. Pärast seda on
                    võimalik sama veebilehe vahendusel jälgida, kuidas tegelikud tulemused kujunevad ning kui täpselt
                    need Sinu või teiste ennustustega kokku langevad.</p>

                <p>Auhinnaks lühiajaline au ja kuulsus.</p>

                <q-btn color="positive" @click="moveToGroupStage" label="Tee oma ennustused »" />
            </q-step>

            <q-step title="Alagrupimängud" subtitle="Kes võidab mängu?">
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

                    <q-btn color="primary" @click="moveToQualifiers" label="Jätka alagrupist edasipääsejate ennustamisega »" :disabled="!groupStageComplete" />
                </template>
                <p v-else>Toimuvate mängude laadimine, oota natuke &hellip;</p>
            </q-step>

            <q-step title="Alagrupist edasipääsejad" subtitle="Millised meeskonnad jätkavad väljalangemismängudega?">
            </q-step>

            <q-step title="Veerandfinalistid" subtitle="Millised meeskonnad jõuavad veerandfinaalidesse?">
            </q-step>

            <q-step title="Poolfinalistid" subtitle="Millised meeskonnad jõuavad poolfinaalidesse?">
            </q-step>

            <q-step title="Finalistid" subtitle="Millised on kaks meeskonda, kelle vahel selgitatakse turniiri võitja?">
            </q-step>

            <q-step title="Maailmameister" subtitle="Milline meeskond on uus maailmameister?">
            </q-step>

            <q-inner-loading :visible="isLoadingStep">
                <q-spinner-puff size="50px" color="primary"></q-spinner-puff>
            </q-inner-loading>
        </q-stepper>
    </q-page>
</template>

<script>
import _ from "lodash"

export default {
    name: "PageIndex",

    computed: {
        groupStageComplete () {
            return !!this.fixtures && _(this.fixtures).every((f) => !!f.result)
        }
    },

    data () {
        return {
            isLoadingStep: false,
            fixtures: null
        }
    },

    methods: {
        async moveToGroupStage () {
            this.isLoadingStep = true
            const response = await this.$axios.get("/predict/fixtures")
            const teams = response.data.teams
            const fixtures = _(response.data.fixtures)
                .map((f) => ({
                    homeTeam: teams[f.homeTeamId],
                    awayTeam: teams[f.awayTeamId],
                    result: null
                }))
                .filter((f) => !!f.homeTeam && !!f.awayTeam)
                .value()
            this.$set(this, "fixtures", fixtures)
            this.isLoadingStep = false
            this.$refs.stepper.next()
        },

        moveToQualifiers () {
            this.isLoadingStep = true

            this.isLoadingStep = false
            this.$refs.stepper.next()
        }
    }
}
</script>
