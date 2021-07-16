import { api } from 'src/boot/axios'
import { computed, defineComponent, ref, watch } from 'vue'

interface ICompetitionFixtureType {
    homeTeamId: number
    awayTeamId: number
    date: string
    externalId: number
}

interface ITeamType {
    name: string
    code: string
    flagUrl: string
    externalId: number
}

interface ICompetitionType {
    id: string
    description: string
    externalId: number
    teams: ITeamType[]
    fixtures: ICompetitionFixtureType[]
    groups: Record<string, number[]>
    version: number
    date: string
}

function initLeague () {
    return {
        name: '',
        code: '',
        competitionId: null
    }
}

export default defineComponent({
    name: 'AddLeague',

    props: {
        isOpen: {
            type: Boolean,
            default: false
        }
    },

    emits: [
        'league-added'
    ],

    setup (props, { emit }) {
        const league = ref(initLeague())
        const competitions = ref<ICompetitionType[]>([])
        const areCompetitionsLoading = ref(false)
        const isSaving = ref(false)

        const competitionOptions = computed(() => {
            return competitions.value.map((x) => ({
                label: x.description,
                value: x.id
            }))
        })

        async function loadCompetitions () {
            areCompetitionsLoading.value = true
            try {
                competitions.value = []
                league.value.competitionId = null
                const response = await api.get<ICompetitionType[]>('/dashboard/competitions')
                competitions.value = response.data
            } finally {
                areCompetitionsLoading.value = false
            }
        }

        async function saveLeague () {
            isSaving.value = true
            try {
                const payload = { id: '', ...league.value }
                const response = await api.post<string>('/leagues/admin/', payload)
                payload.id = response.data
                emit('league-added', payload)
            } finally {
                isSaving.value = false
            }
        }

        watch([props.isOpen], (value) => {
            isSaving.value = false
            league.value = initLeague()
            if (value) {
                void loadCompetitions()
            }
        })

        return {
            competitionOptions,
            saveLeague
        }
    }
})
