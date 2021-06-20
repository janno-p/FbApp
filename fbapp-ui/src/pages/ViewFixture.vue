<template>
    <div class="q-pa-lg">
        <q-inner-loading
            v-if="isInitializing"
            :visible="true"
        >
            <q-spinner-puff
                size="100px"
                color="primary"
            />
            <p class="q-mt-lg">
                Võistluste hetkeseisu küsimine &hellip;
            </p>
        </q-inner-loading>
        <template v-else>
            <div class="row">
                <div class="col-12 col-md-6">
                    <q-list class="q-mx-sm">
                        <q-item>
                            <q-item-side>
                                <q-item-tile>
                                    <q-btn
                                        round
                                        icon="arrow_back"
                                        title="Eelmine mäng"
                                        :disabled="!fixture.previousFixtureId"
                                        @click="openPrevious"
                                    />
                                </q-item-tile>
                            </q-item-side>
                            <q-item-main>
                                <q-item-tile class="text-center">
                                    <div class="q-subtitle text-faded">
                                        {{ fixtureTitle }}
                                    </div>
                                </q-item-tile>
                            </q-item-main>
                            <q-item-side>
                                <q-item-tile>
                                    <q-btn
                                        round
                                        icon="arrow_forward"
                                        title="Järgmine mäng"
                                        :disabled="!fixture.nextFixtureId"
                                        @click="openNext"
                                    />
                                </q-item-tile>
                            </q-item-side>
                        </q-item>
                        <q-item-separator />
                        <q-item
                            v-if="isLoadingFixture"
                            key="loading"
                        >
                            <q-item-main>
                                <q-item-tile>
                                    <q-inner-loading :visible="true">
                                        <q-spinner-puff
                                            size="100px"
                                            color="primary"
                                        />
                                        <p class="q-mt-lg">
                                            Mängu andmete laadimine &hellip;
                                        </p>
                                    </q-inner-loading>
                                </q-item-tile>
                            </q-item-main>
                        </q-item>
                        <template v-else>
                            <q-item key="fixture">
                                <q-item-side>
                                    <q-item-tile class="text-center q-pa-lg">
                                        <img
                                            :src="fixture.homeTeam.flagUrl"
                                            height="32"
                                            :title="fixture.homeTeam.name"
                                        >
                                    </q-item-tile>
                                    <q-item-tile class="text-center">
                                        {{ fixture.homeTeam.name }}
                                    </q-item-tile>
                                </q-item-side>
                                <q-item-main>
                                    <q-item-tile class="text-center text-faded q-caption">
                                        {{ formatStage(fixture.stage) }}
                                    </q-item-tile>
                                    <q-item-tile class="text-center q-py-lg">
                                        <h3 class="q-my-none q-mb-sm">
                                            {{ goals(homeGoals) }} : {{ goals(awayGoals) }}
                                        </h3>
                                        <p
                                            v-if="fixture.penalties"
                                            class="q-body-2 text-faded"
                                        >
                                            (pen {{ fixture.penalties[0] }} : {{ fixture.penalties[1] }} )
                                        </p>
                                    </q-item-tile>
                                    <q-item-tile class="text-center text-faded q-caption">
                                        {{ formatDate(fixture.date) }}
                                    </q-item-tile>
                                </q-item-main>
                                <q-item-side>
                                    <q-item-tile class="text-center q-pa-lg">
                                        <img
                                            :src="fixture.awayTeam.flagUrl"
                                            height="32"
                                            :title="fixture.awayTeam.name"
                                        >
                                    </q-item-tile>
                                    <q-item-tile class="text-center">
                                        {{ fixture.awayTeam.name }}
                                    </q-item-tile>
                                </q-item-side>
                            </q-item>
                            <q-item-separator />
                            <template v-if="fixture.resultPredictions.length > 0">
                                <q-item
                                    v-for="(prediction, j) in fixture.resultPredictions"
                                    :key="j"
                                >
                                    <q-item-side
                                        v-if="isPreFixture"
                                        icon="remove"
                                        class="q-px-md"
                                    />
                                    <q-item-side
                                        v-else-if="isCorrectResultPrediction(prediction)"
                                        icon="done"
                                        color="positive"
                                        class="q-px-md"
                                    />
                                    <q-item-side
                                        v-else
                                        icon="close"
                                        color="negative"
                                        class="q-px-md"
                                    />
                                    <q-item-main>
                                        <q-item-tile>{{ prediction.name }}</q-item-tile>
                                    </q-item-main>
                                    <q-item-side class="q-px-md">
                                        <q-item-tile>{{ predictionText(prediction) }}</q-item-tile>
                                    </q-item-side>
                                </q-item>
                            </template>
                            <template v-if="fixture.qualifierPredictions.length > 0">
                                <q-item
                                    v-for="(prediction, j) in fixture.qualifierPredictions"
                                    :key="j"
                                >
                                    <q-item-side
                                        :icon="homeQualifiesIcon(prediction)"
                                        class="q-px-md"
                                        :color="homeQualifiesResultClass(prediction)"
                                    />
                                    <q-item-main>
                                        <q-item-tile class="text-center">
                                            {{ prediction.name }}
                                        </q-item-tile>
                                    </q-item-main>
                                    <q-item-side
                                        :icon="awayQualifiesIcon(prediction)"
                                        class="q-px-md"
                                        :color="awayQualifiesResultClass(prediction)"
                                    />
                                </q-item>
                            </template>
                        </template>
                    </q-list>
                </div>
            </div>
        </template>
    </div>
</template>

<script>
import ViewFixture from './ViewFixture.vue.ts'
export default ViewFixture
</script>
