<template>
    <q-page class="q-pa-lg">
        <q-table title="Ennustusliigad" :columns="columns" selection="single" :data="data" :loading="isDataLoading" row-key="id" :selected.sync="selected" :pagination.sync="pagination" hide-bottom>
            <div slot="top-right" slot-scope="props">
                <q-btn color="positive" round icon="mdi-plus" @click="addLeague" title="Lisa ennustusliiga" />
            </div>
        </q-table>

        <app-add-league :is-open="isModalOpen" @close="isModalOpen = false" @league-added="leagueAdded" />
    </q-page>
</template>

<script>
import AppAddLeague from "../../components/leagues/add-league"

export default {
    name: "PageDashboardLeagues",

    components: {
        AppAddLeague
    },

    data () {
        return {
            columns: [
                {
                    name: "name",
                    required: true,
                    label: "Nimi",
                    align: "left",
                    field: "name",
                    sortable: "true"
                }
            ],
            data: [],
            selected: [],
            isDataLoading: false,
            isModalOpen: false,
            pagination: {
                sortBy: null,
                descending: false,
                page: 1,
                rowsPerPage: 0
            }
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
            const response = await this.$axios.get("/leagues/admin/")
            this.$set(this, "data", response.data)
            this.isDataLoading = false
        }
    },

    mounted () {
        this.$nextTick(async () => {
            this.reloadTableData()
        })
    }
}
</script>
