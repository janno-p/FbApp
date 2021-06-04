<template>
    <q-page class="q-pa-lg">
        <q-table
            hide-bottom
            row-key="id"
            title="Ennustusliigad"
            :columns="columns"
            :data="data"
            :loading="isDataLoading"
            :pagination.sync="pagination"
        >
            <div slot="top-right">
                <q-btn color="positive" round icon="mdi-plus" @click="addLeague" title="Lisa ennustusliiga" />
            </div>
            <q-tr slot="body" slot-scope="props" :props="props">
                <q-td key="name" :props="props">
                    <div class="row">
                        {{ props.row.name }}
                    </div>
                </q-td>
                <q-td class="text-right">
                    <q-btn size="sm" round dense color="secondary" icon="queue" @click="addPrediction(props.row.id)" class="q-mr-xs" />
                </q-td>
            </q-tr>
        </q-table>

        <app-add-league :is-open="isModalOpen" @close="isModalOpen = false" @league-added="leagueAdded" />

        <q-dialog v-model="addPredictionDialog" prevent-close @cancel="cancelAddPrediction">
            <span slot="title">Lisa ennustus</span>

            <div slot="body">
                <q-field icon="account_circle" :label-width="3">
                    <q-search v-model="terms" float-label="Ennustaja nimi" clearable @clear="val => prediction = null">
                        <q-autocomplete @search="search" @selected="item => prediction = item.record" />
                    </q-search>
                </q-field>
            </div>

            <template slot="buttons" slot-scope="props">
                <q-btn color="primary" label="Salvesta" @click="saveAddPrediction(props.ok)" />
                <q-btn flat label="Katkesta" @click="props.cancel" />
            </template>
        </q-dialog>
    </q-page>
</template>

<script>
import _ from 'lodash'

import AppAddLeague from '../../components/leagues/add-league'

export default {
    name: 'PageDashboardLeagues',

    components: {
        AppAddLeague
    },

    data () {
        return {
            addPredictionDialog: false,
            columns: [
                {
                    name: 'name',
                    required: true,
                    label: 'Nimi',
                    align: 'left',
                    field: 'name',
                    sortable: 'true'
                }
            ],
            data: [],
            isDataLoading: false,
            isModalOpen: false,
            pagination: {
                sortBy: null,
                descending: false,
                page: 1,
                rowsPerPage: 0
            },
            terms: '',
            record: null,
            leagueId: null
        }
    },

    methods: {
        addLeague () {
            this.isModalOpen = true
        },

        leagueAdded (league) {
            this.data.push(league)
            this.isModalOpen = false
        },

        async reloadTableData () {
            this.isDataLoading = true
            const response = await this.$axios.get('/leagues/admin/')
            this.$set(this, 'data', response.data)
            this.isDataLoading = false
        },

        addPrediction (leagueId) {
            this.terms = ''
            this.addPredictionDialog = true
            this.leagueId = leagueId
        },

        cancelAddPrediction () {
            this.terms = ''
            this.addPredictionDialog = false
            this.leagueId = null
        },

        async saveAddPrediction (ok) {
            await this.$axios.post(`/leagues/admin/${this.leagueId}/${this.prediction.id}`, {})
            ok()
        },

        async search (terms, done) {
            const response = await this.$axios.get(`/predictions/admin/search/${terms}`)
            done(_(response.data).map((x) => ({ value: x.name, label: x.name, record: x })).value())
        },

        selected (item) {
            console.info(item)
            console.info(this.terms)
        }
    },

    mounted () {
        this.$nextTick(async () => {
            this.reloadTableData()
        })
    }
}
</script>
