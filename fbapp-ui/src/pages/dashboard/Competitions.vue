<template>
    <q-page class="q-pa-lg">
        <q-table
            v-model:selected="selected"
            v-model:pagination="pagination"
            title="Võistlused"
            :columns="columns"
            selection="single"
            :data="data"
            :loading="isDataLoading"
            row-key="id"
            hide-bottom
        >
            <template #top-right>
                <div>
                    <q-btn
                        title="Lisa võistlus"
                        color="positive"
                        round
                        icon="mdi-plus"
                        @click="addCompetition"
                    />
                </div>
            </template>
        </q-table>

        <AddCompetition
            :is-open="isModalOpen"
            @close="isModalOpen = false"
            @competition-added="competitionAdded"
        />
    </q-page>
</template>

<script>
import DashboardCompetitions from './Competitions.vue.ts'
export default DashboardCompetitions
</script>
