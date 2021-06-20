import { api } from 'src/boot/axios'
import { defineComponent, ref, watch } from 'vue'

interface ICompetitionSourceType {
    label: string
    value: string
}

function initCompetition () {
    return {
        description: '',
        season: 2018,
        dataSource: null,
        date: null
    }
}

export default defineComponent({
    name: 'AddCompetition',

    props: {
        isOpen: {
            type: Boolean,
            default: false
        }
    },

    emits: [
        'competition-added'
    ],

    setup (props, { emit }) {
        const initialYear = new Date().getFullYear()

        const competition = ref(initCompetition())
        const dataSourceOptions = ref<ICompetitionSourceType[]>([])
        const isDataSourceLoading = ref(false)
        const isSaving = ref(false)

        const seasonOptions = [...Array(5).keys()].map((v) => initialYear - v).map((x) => ({ label: x.toString(), value: x }))

        async function loadCompetitionSources (year: number) {
            isDataSourceLoading.value = true
            competition.value.dataSource = null
            dataSourceOptions.value = []
            if (year) {
                const response = await api.get<ICompetitionSourceType[]>(`/dashboard/competition_sources/${year}`)
                dataSourceOptions.value = response.data
            }
            isDataSourceLoading.value = false
        }

        async function saveCompetition () {
            isSaving.value = true
            try {
                const payload = {
                    id: '',
                    description: competition.value.description,
                    externalId: competition.value.dataSource,
                    date: competition.value.date
                }
                const response = await api.post<string>('/dashboard/competition/add', payload)
                payload.id = response.data
                emit('competition-added', payload)
            } finally {
                isSaving.value = false
            }
        }

        watch([props.isOpen], (value) => {
            isSaving.value = false
            competition.value = initCompetition()
            if (value) {
                void loadCompetitionSources(competition.value.season)
            }
        })

        watch(() => competition.value.season, (value) => {
            if (value) {
                void loadCompetitionSources(value)
            }
        })

        return {
            saveCompetition,
            seasonOptions
        }
    }
})
