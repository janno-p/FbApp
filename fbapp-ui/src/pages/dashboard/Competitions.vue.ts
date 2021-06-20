import AddCompetition from 'components/dashboard/AddCompetition.vue'
import { api } from 'src/boot/axios'
import { defineComponent, nextTick, onMounted, ref } from 'vue'

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

export default defineComponent({
    name: 'DashboardCompetitions',

    components: {
        AddCompetition
    },

    setup () {
        const columns = [
            {
                name: 'description',
                required: true,
                label: 'Kirjeldus',
                align: 'left',
                field: 'description',
                sortable: 'true'
            }
        ]

        const data = ref<ICompetitionType[]>([])
        const selected = ref([])
        const isDataLoading = ref(false)
        const isModalOpen = ref(false)

        const pagination = ref({
            sortBy: null,
            descending: false,
            page: 1,
            rowsPerPage: 0
        })

        function addCompetition () {
            isModalOpen.value = true
        }

        function competitionAdded (competition: ICompetitionType) {
            data.value.push(competition)
            isModalOpen.value = false
        }

        async function reloadTableData () {
            isDataLoading.value = true
            const response = await api.get<ICompetitionType[]>('/dashboard/competitions')
            data.value = response.data
            isDataLoading.value = false
        }

        onMounted(() => {
            void nextTick(() => {
                void reloadTableData()
            })
        })

        return {
            addCompetition,
            columns,
            competitionAdded,
            pagination,
            selected
        }
    }
})
