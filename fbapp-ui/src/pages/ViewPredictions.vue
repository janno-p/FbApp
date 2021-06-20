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
                <q-list-header>Alagrupimängud</q-list-header>
                <q-item
                    v-for="fixt in fixtures"
                    :key="fixt.fixture"
                >
                    <q-item-main>
                        <q-item-tile>
                            <q-btn
                                :color="fixt.result === 'HOME' ? 'positive' : undefined"
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
                                :color="fixt.result === 'TIE' ? 'positive' : undefined"
                                round
                                glossy
                                title="Jääb viiki"
                                disabled
                            >
                                =
                            </q-btn>
                            &nbsp;
                            <q-btn
                                :color="fixt.result === 'AWAY' ? 'positive' : undefined"
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
                        </q-item-tile>
                    </q-item-main>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-list-header>Alagruppidest edasipääsejad</q-list-header>
                <q-item
                    v-for="team in roundOf16"
                    :key="team.name"
                >
                    <q-item-side>
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
                    </q-item-side>
                    <q-item-main>
                        <q-item-tile>{{ team.name }}</q-item-tile>
                    </q-item-main>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-list-header>Veerandfinalistid</q-list-header>
                <q-item
                    v-for="team in roundOf8"
                    :key="team.name"
                >
                    <q-item-side>
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
                    </q-item-side>
                    <q-item-main>
                        <q-item-tile>{{ team.name }}</q-item-tile>
                    </q-item-main>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-list-header>Poolfinalistid</q-list-header>
                <q-item
                    v-for="team in roundOf4"
                    :key="team.name"
                >
                    <q-item-side>
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
                    </q-item-side>
                    <q-item-main>
                        <q-item-tile>{{ team.name }}</q-item-tile>
                    </q-item-main>
                </q-item>
            </q-list>

            <q-list class="q-mt-sm">
                <q-list-header>Finalistid</q-list-header>
                <q-item
                    v-for="team in roundOf2"
                    :key="team.name"
                >
                    <q-item-side>
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
                    </q-item-side>
                    <q-item-main>
                        <q-item-tile>{{ winner.name }}</q-item-tile>
                    </q-item-main>
                </q-item>
            </q-list>
        </template>
    </q-page>
</template>

<script>
import ViewPredictions from './ViewPredictions.vue.ts'
export default ViewPredictions
</script>
