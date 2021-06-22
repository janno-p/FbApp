<template>
    <q-page class="q-pa-lg">
        <h4 class="q-mt-none q-mb-sm">
            Minu ennustused
        </h4>
        <q-inner-loading
            v-if="!predictions || isLoadingPredictions"
            :visible="true"
        >
            <q-spinner-puff
                size="100px"
                color="primary"
            />
            <p class="q-mt-lg">
                Varasemate ennustuste kontrollimine &hellip;
            </p>
        </q-inner-loading>
        <template v-else>
            <q-list>
                <q-item>
                    <q-item-label header>
                        Alagrupimängud
                    </q-item-label>
                </q-item>
                <q-item
                    v-for="fixt in fixtures"
                    :key="fixt.fixture"
                >
                    <q-item-label>
                        <q-btn
                            :color="fixt.result === 'HomeWin' ? 'positive' : undefined"
                            round
                            glossy
                            :title="fixt.homeTeam.name"
                            disabled
                        >
                            <img
                                :src="fixt.homeTeam.flagUrl"
                                height="16"
                                :title="fixt.homeTeam.name"
                            >
                        </q-btn>
                        &nbsp;
                        <q-btn
                            :color="fixt.result === 'Tie' ? 'positive' : undefined"
                            round
                            glossy
                            title="Jääb viiki"
                            disabled
                        >
                            =
                        </q-btn>
                        &nbsp;
                        <q-btn
                            :color="fixt.result === 'AwayWin' ? 'positive' : undefined"
                            round
                            glossy
                            :title="fixt.awayTeam.name"
                            disabled
                        >
                            <img
                                :src="fixt.awayTeam.flagUrl"
                                height="16"
                                :title="fixt.awayTeam.name"
                            >
                        </q-btn>
                    </q-item-label>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-item>
                    <q-item-label header>
                        Alagruppidest edasipääsejad
                    </q-item-label>
                </q-item>
                <q-item
                    v-for="team in roundOf16"
                    :key="team.name"
                >
                    <q-item-section side>
                        <q-btn
                            round
                            glossy
                            :title="team.name"
                            disabled
                        >
                            <img
                                :src="team.flagUrl"
                                height="16"
                                :title="team.name"
                            >
                        </q-btn>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>{{ team.name }}</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-item>
                    <q-item-label header>
                        Veerandfinalistid
                    </q-item-label>
                </q-item>
                <q-item
                    v-for="team in roundOf8"
                    :key="team.name"
                >
                    <q-item-section side>
                        <q-btn
                            round
                            glossy
                            :title="team.name"
                            disabled
                        >
                            <img
                                :src="team.flagUrl"
                                height="16"
                                :title="team.name"
                            >
                        </q-btn>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>{{ team.name }}</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-item>
                    <q-item-label header>
                        Poolfinalistid
                    </q-item-label>
                </q-item>
                <q-item
                    v-for="team in roundOf4"
                    :key="team.name"
                >
                    <q-item-section side>
                        <q-btn
                            round
                            glossy
                            :title="team.name"
                            disabled
                        >
                            <img
                                :src="team.flagUrl"
                                height="16"
                                :title="team.name"
                            >
                        </q-btn>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>{{ team.name }}</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-item>
                    <q-item-label header>
                        Finalistid
                    </q-item-label>
                </q-item>
                <q-item
                    v-for="team in roundOf2"
                    :key="team.name"
                >
                    <q-item-section side>
                        <q-btn
                            round
                            glossy
                            :title="team.name"
                            disabled
                        >
                            <img
                                :src="team.flagUrl"
                                height="16"
                                :title="team.name"
                            >
                        </q-btn>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>{{ team.name }}</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-item>
                    <q-item-label header>
                        Turniiri võitja
                    </q-item-label>
                </q-item>
                <q-item>
                    <q-item-section side>
                        <q-btn
                            round
                            glossy
                            :title="winner.name"
                            disabled
                        >
                            <img
                                :src="winner.flagUrl"
                                height="16"
                                :title="winner.name"
                            >
                        </q-btn>
                    </q-item-section>
                    <q-item-section>
                        <q-item-label>{{ winner.name }}</q-item-label>
                    </q-item-section>
                </q-item>
            </q-list>
        </template>
    </q-page>
</template>

<script>
import ViewPredictions from './ViewPredictions.vue.ts'
export default ViewPredictions
</script>
