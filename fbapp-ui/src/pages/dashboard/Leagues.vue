<template>
    <q-page class="q-pa-lg">
        <q-table
            v-model:pagination="pagination"
            hide-bottom
            row-key="id"
            title="Ennustusliigad"
            :columns="columns"
            :data="data"
            :loading="isDataLoading"
        >
            <template #top-right>
                <div>
                    <q-btn
                        color="positive"
                        round
                        icon="mdi-plus"
                        title="Lisa ennustusliiga"
                        @click="addLeague"
                    />
                </div>
            </template>
            <template #body="props">
                <q-tr :props="props">
                    <q-td
                        key="name"
                        :props="props"
                    >
                        <div class="row">
                            {{ props.row.name }}
                        </div>
                    </q-td>
                    <q-td class="text-right">
                        <q-btn
                            class="q-mr-xs"
                            size="sm"
                            round
                            dense
                            color="secondary"
                            icon="queue"
                            @click="addPrediction(props.row.id)"
                        />
                    </q-td>
                </q-tr>
            </template>
        </q-table>

        <app-add-league
            :is-open="isModalOpen"
            @close="isModalOpen = false"
            @league-added="leagueAdded"
        />

        <q-dialog
            v-model="addPredictionDialog"
            prevent-close
            @cancel="cancelAddPrediction"
        >
            <template #title>
                <span>Lisa ennustus</span>
            </template>

            <template #body>
                <div>
                    <q-field
                        icon="account_circle"
                        :label-width="3"
                    >
                        <q-search
                            v-model="terms"
                            float-label="Ennustaja nimi"
                            clearable
                            @clear="clearPrediction"
                        >
                            <q-autocomplete
                                @search="search"
                                @selected="setPrediction"
                            />
                        </q-search>
                    </q-field>
                </div>
            </template>

            <template #buttons="props">
                <q-btn
                    color="primary"
                    label="Salvesta"
                    @click="saveAddPrediction(props.ok)"
                />
                <q-btn
                    flat
                    label="Katkesta"
                    @click="props.cancel"
                />
            </template>
        </q-dialog>
    </q-page>
</template>

<script>
import DashboardLeagues from './Leagues.vue.ts'
export default DashboardLeagues
</script>
