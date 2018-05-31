<template>
    <q-page class="q-pa-lg">
        <q-table title="Võistlused" :columns="columns" selection="single">
            <div slot="top-right" slot-scope="props">
                <q-btn color="positive" round icon="mdi-plus" @click="addCompetition" title="Lisa võistlus" />
            </div>
        </q-table>

        <app-add-competition :is-open="isModalOpen" @close="isModalOpen = false" />
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
                    name: "name",
                    required: true,
                    label: "Nimi",
                    align: "left",
                    field: "name",
                    sortable: "true"
                }
            ],
            isModalOpen: false
        }
    },

    methods: {
        addCompetition () {
            this.isModalOpen = true
        }
    }
}
</script>
