<template>
    <q-modal v-model="isOpen" no-backdrop-dismiss :content-css="{ minWidth: '500px', minHeight: '360px' }">
        <q-modal-layout>
            <q-toolbar slot="header">
                <q-toolbar-title>Võistluse lisamine</q-toolbar-title>
                <q-btn flat round dense @click="$emit('close')" icon="mdi-window-close" />
            </q-toolbar>

            <q-toolbar slot="footer" color="light">
                <q-toolbar-title />
                <q-btn label="Salvesta" color="positive" icon="mdi-check-outline" @click="saveLeague" :loading="isSaving">
                    <q-spinner-pie slot="loading" />
                </q-btn>
            </q-toolbar>

            <div class="q-pa-md" v-if="!!league">
                <q-field icon="mdi-calendar-text">
                    <q-spinner-puff v-if="areCompetitionsLoading" color="primary" :size="30" />
                    <q-select v-else float-label="Võistlus" v-model="league.competitionId" :options="competitionOptions" />
                </q-field>
                <q-field icon="mdi-sign-text" class="q-mt-md">
                    <q-input float-label="Ennustusliiga nimi" v-model="league.name" />
                </q-field>
                <q-field icon="mdi-calendar-text" class="q-mt-md">
                    <q-input float-label="Ennustusliiga kood" v-model="league.code" />
                </q-field>
            </div>
        </q-modal-layout>
    </q-modal>
</template>

<script>
import _ from "lodash"

function initLeague () {
    return {
        name: "",
        code: "",
        competitionId: null
    }
}

export default {
    name: "AppAddLeague",

    computed: {
        competitionOptions () {
            return _(this.competitions).map((x) => ({
                label: x.description,
                value: x.id
            })).value()
        }
    },

    data () {
        return {
            league: initLeague(),
            competitions: [],
            areCompetitionsLoading: false,
            isSaving: false
        }
    },

    methods: {
        async loadCompetitions () {
            this.areCompetitionsLoading = true
            try {
                this.$set(this, "competitions", null)
                this.league.competitionId = null
                const response = await this.$axios.get("/dashboard/competitions")
                this.$set(this, "competitions", response.data)
            } finally {
                this.areCompetitionsLoading = false
            }
        },

        async saveLeague () {
            this.isSaving = true
            try {
                const payload = _(this.league).clone()
                const response = await this.$axios.post("/leagues/admin/", payload)
                payload.id = response.data
                this.$emit("league-added", payload)
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
            this.$set(this, "league", value ? initLeague() : null)
            if (value) {
                await this.loadCompetitions()
            }
        }
    }
}
</script>
