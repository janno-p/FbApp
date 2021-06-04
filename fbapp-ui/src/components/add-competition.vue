<template>
    <q-modal v-model="isOpenWrapper" no-backdrop-dismiss :content-css="{ minWidth: '500px', minHeight: '360px' }">
        <q-modal-layout>
            <q-toolbar slot="header">
                <q-toolbar-title>Võistluse lisamine</q-toolbar-title>
                <q-btn flat round dense @click="$emit('close')" icon="mdi-window-close" />
            </q-toolbar>

            <q-toolbar slot="footer" color="light">
                <q-toolbar-title />
                <q-btn label="Salvesta" color="positive" icon="mdi-check-outline" @click="saveCompetition" :loading="isSaving">
                    <q-spinner-pie slot="loading" />
                </q-btn>
            </q-toolbar>

            <div class="q-pa-md" v-if="!!competition">
                <q-field icon="mdi-sign-text">
                    <q-input float-label="Võistluse nimetus" v-model="competition.description" />
                </q-field>
                <q-field icon="mdi-calendar-text" class="q-mt-md">
                    <q-select float-label="Võistluse hooaeg" v-model="competition.season" :options="seasonOptions" />
                </q-field>
                <q-field icon="mdi-import" class="q-mt-md">
                    <q-spinner-puff v-if="isDataSourceLoading" color="primary" :size="30" />
                    <q-select v-else float-label="Tulemuste sisendvoog" v-model="competition.dataSource" :options="dataSourceOptions" />
                </q-field>
                <q-field icon="mdi-calendar-clock" class="q-mt-md">
                    <q-datetime v-model="competition.date" type="datetime" :first-day-of-week="1" :format24h="true" float-label="Võistluste algus" format="DD.MM.YYYY HH:mm" :modal="true" />
                </q-field>
            </div>
        </q-modal-layout>
    </q-modal>
</template>

<script>
import _ from 'lodash'

function initCompetition () {
    return {
        description: '',
        season: 2018,
        dataSource: null,
        date: null
    }
}

export default {
    name: 'AppAddCompetition',

    data () {
        const initialYear = new Date().getFullYear()
        return {
            competition: initCompetition(),
            seasonOptions: _(_.range(initialYear, initialYear - 5, -1)).map((x) => ({ label: x.toString(), value: x })).value(),
            dataSourceOptions: [],
            isDataSourceLoading: false,
            isSaving: false
        }
    },

    computed: {
        isOpenWrapper: {
            get () {
                return this.isOpen
            },
            set (value) {
                this.$emit('update:isOpen', value)
            }
        }
    },

    methods: {
        async loadCompetitionSources (year) {
            this.isDataSourceLoading = true
            this.competition.dataSource = null
            this.$set(this, 'dataSourceOptions', [])
            if (year) {
                const response = await this.$axios.get(`/dashboard/competition_sources/${year}`)
                this.$set(this, 'dataSourceOptions', response.data)
            }
            this.isDataSourceLoading = false
        },

        async saveCompetition () {
            this.isSaving = true
            try {
                const payload = {
                    description: this.competition.description,
                    externalId: this.competition.dataSource,
                    date: this.competition.date
                }
                const response = await this.$axios.post('/dashboard/competition/add', payload)
                payload.id = response.data
                this.$emit('competition-added', payload)
            } finally {
                this.isSaving = false
            }
        }
    },

    props: {
        isOpen: {
            type: Boolean,
            default: false
        }
    },

    watch: {
        async isOpen (value) {
            this.isSaving = false
            this.$set(this, 'competition', value ? initCompetition() : null)
            if (value) {
                await this.loadCompetitionSources(this.competition.season)
            }
        },

        'competition.season' (value) {
            return value ? this.loadCompetitionSources(value) : []
        }
    }
}
</script>
