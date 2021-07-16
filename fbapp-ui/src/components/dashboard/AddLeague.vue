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
                        @click="saveLeague"
                    >
                        <template #loading>
                            <q-spinner-pie />
                        </template>
                    </q-btn>
                </q-toolbar>
            </template>

            <div
                v-if="!!league"
                class="q-pa-md"
            >
                <q-field icon="mdi-calendar-text">
                    <q-spinner-puff
                        v-if="areCompetitionsLoading"
                        color="primary"
                        :size="30"
                    />
                    <q-select
                        v-else
                        v-model="league.competitionId"
                        float-label="Võistlus"
                        :options="competitionOptions"
                    />
                </q-field>
                <q-field
                    icon="mdi-sign-text"
                    class="q-mt-md"
                >
                    <q-input
                        v-model="league.name"
                        float-label="Ennustusliiga nimi"
                    />
                </q-field>
                <q-field
                    icon="mdi-calendar-text"
                    class="q-mt-md"
                >
                    <q-input
                        v-model="league.code"
                        float-label="Ennustusliiga kood"
                    />
                </q-field>
            </div>
        </q-modal-layout>
    </q-modal>
</template>

<script>
import AddLeague from './AddLeague.vue.ts'
export default AddLeague
</script>
