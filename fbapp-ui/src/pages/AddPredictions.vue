<template>
    <q-page class="q-pa-lg">
        <q-stepper
            ref="stepper"
            v-model="currentStep"
            vertical
            no-header-navigation
        >
            <q-step
                :name="0"
                default
                title="Ennustusmäng"
                subtitle="Veel on aega"
            >
                <p>
                    Ajavahemikus 14. juunist 15. juulini toimuvad Venemaal 2018. aasta jalgpalli
                    maailmameistrivõistlused. Lisaks rahvusmeeskondade mõõduvõtmistele pakub antud veebileht
                    omavahelist võistlusmomenti ka tugitoolisportlastele tulemuste ennustamise näol.
                </p>

                <p>
                    Oma eelistusi saad valida ja muuta kuni avamänguni 14. juunil kell 18:00. Pärast seda on
                    võimalik sama veebilehe vahendusel jälgida, kuidas tegelikud tulemused kujunevad ning kui täpselt
                    need Sinu või teiste ennustustega kokku langevad.
                </p>

                <p>Auhinnaks lühiajaline au ja kuulsus.</p>

                <q-btn
                    color="positive"
                    label="Tee oma ennustused »"
                    @click="moveToGroupStage"
                />
            </q-step>

            <q-step
                :name="1"
                title="Alagrupimängud"
                subtitle="Kes võidab mängu?"
            >
                <template v-if="!!fixtures">
                    <div class="row q-pa-md">
                        <div
                            v-for="(f, i) in fixtures"
                            :key="i"
                            class="q-py-md col-2 col-lg-3 col-md-4 col-sm-6 col-xs-12"
                        >
                            <q-btn
                                :color="f.result === 'HOME' ? (f.isManual ? 'positive' : 'info') : undefined"
                                round
                                glossy
                                :title="f.homeTeam.name"
                                @click="setFixtureResult(f, 'HOME')"
                            >
                                <img
                                    :src="f.homeTeam.flagUrl"
                                    height="16"
                                >
                            </q-btn>
                            &nbsp;
                            <q-btn
                                :color="f.result === 'TIE' ? (f.isManual ? 'positive' : 'info') : undefined"
                                round
                                title="Jääb viiki"
                                @click="setFixtureResult(f, 'TIE')"
                            >
                                =
                            </q-btn>
                            &nbsp;
                            <q-btn
                                :color="f.result === 'AWAY' ? (f.isManual ? 'positive' : 'info') : undefined"
                                round
                                glossy
                                :title="f.awayTeam.name"
                                @click="setFixtureResult(f, 'AWAY')"
                            >
                                <img
                                    :src="f.awayTeam.flagUrl"
                                    height="16"
                                >
                            </q-btn>
                        </div>
                    </div>

                    <q-btn
                        color="primary"
                        label="Jätka alagrupist edasipääsejate ennustamisega »"
                        :disabled="!groupStageComplete"
                        @click="moveToNextQualRound"
                    />
                </template>
                <p v-else>
                    Toimuvate mängude laadimine, oota natuke &hellip;
                </p>
            </q-step>

            <template v-for="(c, i) in steps">
                <q-step
                    v-if="!!c"
                    :key="i"
                    :name="i"
                    :title="c.title"
                    :subtitle="c.subtitle"
                >
                    <template v-if="currentStep === i">
                        <div class="row q-pb-md">
                            <div
                                v-for="(g, j) in qualifiers[i].teams"
                                :key="j"
                                class="q-pa-xs col-2 col-lg-3 col-md-4 col-sm-6 col-xs-12"
                            >
                                <q-list>
                                    <q-list-header>{{ toUpperCase(j) }} alagrupp</q-list-header>
                                    <q-item
                                        v-for="(x, k) in g"
                                        :key="k"
                                    >
                                        <q-item-side>
                                            <q-btn
                                                :color="getColor(x)"
                                                :disabled="isDisabled(x)"
                                                round
                                                glossy
                                                title="Vali võistkond"
                                                @click="changeSelection(x)"
                                            >
                                                <img
                                                    :src="x.team.flagUrl"
                                                    height="16"
                                                >
                                            </q-btn>
                                        </q-item-side>
                                        <q-item-main>
                                            <q-item-tile>{{ x.team.name }}</q-item-tile>
                                        </q-item-main>
                                    </q-item>
                                </q-list>
                            </div>
                        </div>

                        <q-btn
                            v-if="currentStep < 6"
                            color="primary"
                            :label="c.buttonText"
                            :disabled="!qualifiers[i].isFull"
                            @click="moveToNextQualRound"
                        />
                        <q-btn
                            v-else-if="isSignedIn"
                            color="positive"
                            label="Registreeri oma ennustus"
                            :disabled="!qualifiers[i].isFull"
                            :loading="isSaveInProgress"
                            @click="registerPrediction"
                        >
                            <template #loading>
                                <q-spinner-pie />
                            </template>
                        </q-btn>
                        <q-btn
                            v-else
                            color="positive"
                            icon="mdi-google"
                            label="Registreeri oma ennustus Google kontoga"
                            :disabled="!qualifiers[i].isFull"
                            :loading="isSaveInProgress"
                            @click="registerPrediction"
                        >
                            <template #loading>
                                <q-spinner-pie />
                            </template>
                        </q-btn>
                    </template>
                </q-step>
            </template>

            <q-inner-loading :visible="isLoadingStep">
                <q-spinner-puff
                    size="50px"
                    color="primary"
                />
            </q-inner-loading>
        </q-stepper>

        <q-page-sticky
            v-if="displayRandomizer"
            class="text-center"
            position="bottom"
            :offset="[18, 18]"
        >
            <div
                v-if="displayCounter"
                class="q-mb-sm"
            >
                <q-rating
                    readonly
                    :max="counterValue"
                    :value="counterValue"
                    color="positive"
                />
            </div>
            <div>
                <q-btn
                    icon="mdi-dice-multiple"
                    color="teal"
                    label="Vali suvaliselt"
                    @click="randomize"
                />
            </div>
        </q-page-sticky>
    </q-page>
</template>

<script>
import AddPredictions from './AddPredictions.vue.ts'
export default AddPredictions
</script>
