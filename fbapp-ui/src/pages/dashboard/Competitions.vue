<template>
    <q-page class="q-pa-lg">
        <q-table
            v-model:selected="selected"
            v-model:pagination="pagination"
            title="Võistlused"
            :columns="columns"
            selection="single"
            :rows="rows"
            :loading="isDataLoading"
            row-key="id"
            hide-bottom
        >
            <template #top-right>
                <q-btn
                    color="positive"
                    size="md"
                    label="Lisa võistlus"
                    title="Lisa võistlus"
                    no-caps
                    @click="addCompetition"
                />
            </template>
        </q-table>

        <AddCompetition
            v-if="isModalOpen"
            v-model="isModalOpen"
            @competition-added="competitionAdded"
        />
    </q-page>
</template>

<script>
import DashboardCompetitions from './Competitions.vue.ts'
export default DashboardCompetitions
</script>
