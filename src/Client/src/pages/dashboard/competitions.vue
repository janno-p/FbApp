<template>
    <q-page class="q-pa-lg">
        <q-table title="Võistlused" :columns="columns" selection="single">
            <div slot="top-right" slot-scope="props">
                <q-btn color="positive" round icon="mdi-plus" @click="addCompetition" title="Lisa võistlus" />
            </div>
        </q-table>

        <q-modal v-model="isModalOpen" :content-css="{minWidth: '80vw', minHeight: '80vh'}" no-backdrop-dismiss>
            <q-modal-layout content-class="q-pa-md">
                <q-toolbar slot="header">
                    <q-toolbar-title>Võistluse lisamine</q-toolbar-title>
                    <q-btn flat round dense @click="isModalOpen = false" icon="mdi-window-close" />
                </q-toolbar>

                <div>
                    <p>&lt; Võistluse lisamise vorm &gt;</p>
                </div>
            </q-modal-layout>
        </q-modal>
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
            ],
            isModalOpen: false
        }
    },

    methods: {
        addCompetition () {
            this.isModalOpen = true
            console.info("competition added ...")
        }
    }
}
</script>
