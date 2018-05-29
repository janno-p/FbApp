<template>
    <q-page class="q-pa-lg">
        <q-table title="Võistlused" :columns="columns" selection="single">
            <div slot="top-right" slot-scope="props">
                <q-btn color="positive" round icon="mdi-plus" @click="addCompetition" title="Lisa võistlus" />
            </div>
        </q-table>
    </q-page>
</template>

<script>
export default {
    name: "PageDashboardCompetitions",

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
            ]
        }
    },

    methods: {
        addCompetition () {
            console.info("competition added ...")
        }
    }
}
</script>
