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
                    <q-list
                        bordered
                        class="q-mx-sm"
                    >
                        <q-item class="q-pa-md">
                            <q-item-section side>
                                <q-btn
                                    v-if="fixture.previousFixtureId"
                                    type="a"
                                    round
                                    icon="arrow_back"
                                    title="Eelmine mäng"
                                    :to="{ name: 'fixture', params: { fixtureId: fixture.previousFixtureId } }"
                                />
                            </q-item-section>
                            <q-item-section>
                                <q-item-label
                                    header
                                    class="text-center"
                                >
                                    {{ fixtureTitle }}
                                </q-item-label>
                            </q-item-section>
                            <q-item-section side>
                                <q-btn
                                    v-if="fixture.nextFixtureId"
                                    type="a"
                                    round
                                    icon="arrow_forward"
                                    title="Järgmine mäng"
                                    :to="{ name: 'fixture', params: { fixtureId: fixture.nextFixtureId } }"
                                />
                            </q-item-section>
                        </q-item>
                        <q-separator />
                        <q-item
                            v-if="isLoadingFixture"
                            key="loading"
                            class="q-pa-md"
                        >
                            <q-item-section>
                                <q-inner-loading :visible="true">
                                    <q-spinner-puff
                                        size="100px"
                                        color="primary"
                                    />
                                    <p class="q-mt-lg">
                                        Mängu andmete laadimine &hellip;
                                    </p>
                                </q-inner-loading>
                            </q-item-section>
                        </q-item>
                        <template v-else>
                            <q-item
                                key="fixture"
                                class="q-pa-md"
                            >
                                <q-item-section side>
                                    <div class="text-center q-pa-lg">
                                        <img
                                            :src="fixture.homeTeam.flagUrl"
                                            height="32"
                                            :title="fixture.homeTeam.name"
                                        >
                                    </div>
                                    <q-item-label class="q-mx-auto">
                                        {{ fixture.homeTeam.name }}
                                    </q-item-label>
                                </q-item-section>
                                <q-item-section>
                                    <q-item-label
                                        caption
                                        class="text-center"
                                    >
                                        {{ formatStage(fixture.stage) }}
                                    </q-item-label>
                                    <q-item-label class="text-center q-py-lg">
                                        <h3 class="q-my-none q-mb-sm">
                                            {{ goals(homeGoals) }} : {{ goals(awayGoals) }}
                                        </h3>
                                        <p
                                            v-if="fixture.penalties"
                                            class="q-body-2 text-faded"
                                        >
                                            (pen {{ fixture.penalties[0] }} : {{ fixture.penalties[1] }} )
                                        </p>
                                    </q-item-label>
                                    <q-item-label
                                        caption
                                        class="text-center"
                                    >
                                        {{ formatDate(fixture.date) }}
                                    </q-item-label>
                                </q-item-section>
                                <q-item-section
                                    side
                                >
                                    <div class="q-pa-lg">
                                        <img
                                            :src="fixture.awayTeam.flagUrl"
                                            height="32"
                                            :title="fixture.awayTeam.name"
                                        >
                                    </div>
                                    <q-item-label class="q-mx-auto">
                                        {{ fixture.awayTeam.name }}
                                    </q-item-label>
                                </q-item-section>
                            </q-item>
                            <q-separator />
                            <template v-if="fixture.resultPredictions.length > 0">
                                <q-item
                                    v-for="(prediction, j) in fixture.resultPredictions"
                                    :key="j"
                                    class="q-px-md"
                                >
                                    <q-item-section
                                        class="q-px-md"
                                        side
                                    >
                                        <q-icon
                                            v-if="isPreFixture"
                                            name="remove"
                                        />
                                        <q-icon
                                            v-else-if="isCorrectResultPrediction(prediction)"
                                            name="done"
                                            color="positive"
                                        />
                                        <q-icon
                                            v-else
                                            name="close"
                                            color="negative"
                                        />
                                    </q-item-section>
                                    <q-item-section>
                                        <q-item-label>{{ prediction.name }}</q-item-label>
                                    </q-item-section>
                                    <q-item-section
                                        side
                                    >
                                        <q-item-label class="q-px-md">
                                            {{ predictionText(prediction) }}
                                        </q-item-label>
                                    </q-item-section>
                                </q-item>
                            </template>
                            <template v-if="fixture.qualifierPredictions.length > 0">
                                <q-item
                                    v-for="(prediction, j) in fixture.qualifierPredictions"
                                    :key="j"
                                    class="q-pa-md"
                                >
                                    <q-item-section
                                        side
                                        class="q-px-md"
                                    >
                                        <q-icon
                                            :name="homeQualifiesIcon(prediction)"
                                            :color="homeQualifiesResultClass(prediction)"
                                        />
                                    </q-item-section>
                                    <q-item-section>
                                        <q-item-label class="text-center">
                                            {{ prediction.name }}
                                        </q-item-label>
                                    </q-item-section>
                                    <q-item-section
                                        side
                                        class="q-px-md"
                                    >
                                        <q-icon
                                            :name="awayQualifiesIcon(prediction)"
                                            :color="awayQualifiesResultClass(prediction)"
                                        />
                                    </q-item-section>
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
