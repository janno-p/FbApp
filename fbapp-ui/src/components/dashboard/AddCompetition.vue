<template>
    <q-dialog
        v-model="modelValueProxy"
        no-backdrop-dismiss
    >
        <q-card style="min-width: 500px">
            <q-card-section class="row items-center q-pb-none">
                <div class="text-h6">Võistluse lisamine</div>
                <q-space />
                <q-btn v-close-popup icon="close" flat round dense />
            </q-card-section>

            <div class="q-pa-md">
                <q-input
                    v-model="description"
                    outlined
                    label="Võistluse nimetus"
                >
                    <template #prepend>
                        <q-icon name="mdi-sign-text" />
                    </template>
                </q-input>

                <q-select
                    v-model="season"
                    class="q-mt-sm"
                    outlined
                    label="Võistluse hooaeg"
                    :options="seasonOptions"
                >
                    <template #prepend>
                        <q-icon name="mdi-calendar-text" />
                    </template>
                </q-select>

                <q-select
                    v-model="dataSource"
                    class="q-mt-sm"
                    outlined
                    label="Tulemuste sisendvoog"
                    :options="dataSourceOptions"
                    :disable="isDataSourceLoading"
                >
                    <template #prepend>
                        <q-spinner-puff
                            v-if="isDataSourceLoading"
                            color="primary"
                            :size="30"
                        />
                        <q-icon
                            v-else
                            name="mdi-import"
                        />
                    </template>
                </q-select>

                <DateTimeInput
                    v-model="date"
                    class="q-mt-sm"
                    type="datetime"
                    :first-day-of-week="1"
                    :format24h="true"
                    label="Võistluse algus"
                    format="DD.MM.YYYY HH:mm"
                    :modal="true"
                />
            </div>

            <q-card-actions align="right">
                <q-btn
                    flat
                    color="primary"
                    icon="mdi-check-outline"
                    label="Salvesta"
                    :loading="isSaving"
                    @click="saveCompetition"
                >
                    <template #loading>
                        <q-spinner-pie />
                    </template>
                </q-btn>
            </q-card-actions>
        </q-card>
    </q-dialog>
</template>

<script src="./AddCompetition.vue.ts" />
