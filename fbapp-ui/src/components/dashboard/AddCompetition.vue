<template>
    <q-modal
        v-model="isOpen"
        no-backdrop-dismiss
        :content-css="{ minWidth: '500px', minHeight: '360px' }"
    >
        <q-modal-layout>
            <template #header>
                <q-toolbar>
                    <q-toolbar-title>Võistluse lisamine</q-toolbar-title>
                    <q-btn
                        flat
                        round
                        dense
                        icon="mdi-window-close"
                        @click="$emit('close')"
                    />
                </q-toolbar>
            </template>

            <template #footer>
                <q-toolbar color="light">
                    <q-toolbar-title />
                    <q-btn
                        label="Salvesta"
                        color="positive"
                        icon="mdi-check-outline"
                        :loading="isSaving"
                        @click="saveCompetition"
                    >
                        <template #loading>
                            <q-spinner-pie />
                        </template>
                    </q-btn>
                </q-toolbar>
            </template>

            <div
                v-if="!!competition"
                class="q-pa-md"
            >
                <q-field icon="mdi-sign-text">
                    <q-input
                        v-model="competition.description"
                        float-label="Võistluse nimetus"
                    />
                </q-field>
                <q-field
                    icon="mdi-calendar-text"
                    class="q-mt-md"
                >
                    <q-select
                        v-model="competition.season"
                        float-label="Võistluse hooaeg"
                        :options="seasonOptions"
                    />
                </q-field>
                <q-field
                    icon="mdi-import"
                    class="q-mt-md"
                >
                    <q-spinner-puff
                        v-if="isDataSourceLoading"
                        color="primary"
                        :size="30"
                    />
                    <q-select
                        v-else
                        v-model="competition.dataSource"
                        float-label="Tulemuste sisendvoog"
                        :options="dataSourceOptions"
                    />
                </q-field>
                <q-field
                    icon="mdi-calendar-clock"
                    class="q-mt-md"
                >
                    <q-datetime
                        v-model="competition.date"
                        type="datetime"
                        :first-day-of-week="1"
                        :format24h="true"
                        float-label="Võistluste algus"
                        format="DD.MM.YYYY HH:mm"
                        :modal="true"
                    />
                </q-field>
            </div>
        </q-modal-layout>
    </q-modal>
</template>

<script>
import AddCompetition from './AddCompetition.vue.ts'
export default AddCompetition
</script>
