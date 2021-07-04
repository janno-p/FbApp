import AddLeague from 'components/dashboard/AddLeague.vue'
import { api } from 'src/boot/axios'
import { defineComponent, nextTick, onMounted, ref } from 'vue'

interface ILeagueType {
    id: string
    name: string
    code: string
    competitionId: string
}

interface IPredictionItemType {
    id: string
    name: string
}

export default defineComponent({
    name: 'DashboardLeagues',

    components: {
        AddLeague
    },

    setup () {
        const addPredictionDialog = ref(false)

        const columns = [
            {
                name: 'name',
                required: true,
                label: 'Nimi',
                align: 'left',
                field: 'name',
                sortable: 'true'
            }
        ]

        const data = ref<ILeagueType[]>([])
        const isDataLoading = ref(false)
        const isModalOpen = ref(false)

        const pagination = ref({
            sortBy: null,
            descending: false,
            page: 1,
            rowsPerPage: 0
        })

        const terms = ref('')
        const prediction = ref<IPredictionItemType>()
        const leagueId = ref<string>()

        function clearPrediction () {
            prediction.value = undefined
        }

        function addLeague () {
            isModalOpen.value = true
        }

        function leagueAdded (league: ILeagueType) {
            data.value.push(league)
            isModalOpen.value = false
        }

        async function reloadTableData () {
            isDataLoading.value = true
            const response = await api.get<ILeagueType[]>('/leagues/admin/')
            data.value = response.data
            isDataLoading.value = false
        }

        function setPrediction (model: { value: string, label: string, record: IPredictionItemType }) {
            prediction.value = model.record
        }

        function addPrediction (value: string) {
            terms.value = ''
            addPredictionDialog.value = true
            leagueId.value = value
        }

        function cancelAddPrediction () {
            terms.value = ''
            addPredictionDialog.value = false
            leagueId.value = undefined
        }

        async function saveAddPrediction (ok: () => void) {
            await api.post(`/leagues/admin/${leagueId.value ?? ''}/${prediction.value?.id ?? ''}`, {})
            ok()
        }

        async function search (terms: string, done: (_: { value: string, label: string, record: IPredictionItemType }[]) => void) {
            const response = await api.get<IPredictionItemType[]>(`/predictions/admin/search/${terms}`)
            done(response.data.map((x) => ({ value: x.name, label: x.name, record: x })))
        }

        onMounted(() => {
            void nextTick(() => {
                void reloadTableData()
            })
        })

        return {
            addLeague,
            addPrediction,
            cancelAddPrediction,
            clearPrediction,
            columns,
            leagueAdded,
            pagination,
            prediction,
            saveAddPrediction,
            search,
            setPrediction
        }
    }
})
