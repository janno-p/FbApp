<template>
    <q-page class="q-pa-lg">
        <q-table title="Võistlused" :columns="columns" selection="single" :data="data" :loading="isDataLoading" row-key="id" :selected.sync="selected" :pagination.sync="pagination" hide-bottom>
            <div slot="top-right" slot-scope="props">
                <q-btn color="positive" round icon="mdi-plus" @click="addCompetition" title="Lisa võistlus" />
            </div>
        </q-table>

        <app-add-competition :is-open="isModalOpen" @close="isModalOpen = false" @competition-added="competitionAdded" />
    </q-page>
</template>

<script>
import AppAddCompetition from "../../components/add-competition"

export default {
    name: "PageDashboardCompetitions",

    components: {AppAddCompetition},

    data () {
        return {
            columns: [
                {
                    name: "description",
                    required: true,
                    label: "Kirjeldus",
                    align: "left",
                    field: "description",
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
        addCompetition () {
            this.isModalOpen = true
        },

        competitionAdded (competition) {
            this.data.push(competition)
            this.isModalOpen = false
        },

        async reloadTableData () {
            this.isDataLoading = true
            const response = await this.$axios.get("/dashboard/competitions")
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
