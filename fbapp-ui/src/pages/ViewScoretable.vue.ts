import { api } from 'src/boot/axios'
import { defineComponent, onMounted, ref } from 'vue'

interface IPredictionScoreType {
    id: string
    name: string
    points: number[]
    total: number
    ratio: number
}

interface IPredictionScoreWithResponseType extends IPredictionScoreType {
    position: number
}

export default defineComponent({
    name: 'ViewScoretable',

    setup () {
        const isLoading = ref(true)
        const standings = ref<IPredictionScoreWithResponseType[]>([])

        const columns = [
            {
                name: 'ratio',
                required: true,
                label: '',
                align: 'center',
                field: 'ratio',
                sortable: false
            },
            {
                name: 'pos',
                required: true,
                label: '',
                align: 'left',
                field: 'position',
                sortable: true
            },
            {
                name: 'name',
                required: true,
                label: 'Nimi',
                align: 'left',
                field: 'name',
                sortable: true
            },
            {
                name: 'group_stage',
                label: 'Alagrupivoor',
                align: 'right',
                field: (r: IPredictionScoreType) => r.points[0],
                sortable: true
            },
            {
                name: 'round_of_16',
                label: 'Edasipääsejad',
                align: 'right',
                field: (r: IPredictionScoreType) => r.points[1],
                sortable: true
            },
            {
                name: 'quarter_finals',
                label: 'Veerandfinalistid',
                align: 'right',
                field: (r: IPredictionScoreType) => r.points[2],
                sortable: true
            },
            {
                name: 'semi_finals',
                label: 'Poolfinalistid',
                align: 'right',
                field: (r: IPredictionScoreType) => r.points[3],
                sortable: true
            },
            {
                name: 'final',
                label: 'Finalistid',
                align: 'right',
                field: (r: IPredictionScoreType) => r.points[4],
                sortable: true
            },
            {
                name: 'winner',
                label: 'Võitja',
                align: 'right',
                field: (r: IPredictionScoreType) => r.points[5],
                sortable: true
            },
            {
                name: 'total',
                label: 'Kokku',
                align: 'right',
                field: 'total',
                sortable: true
            }
        ]

        const pagination = ref({
            sortBy: 'total',
            descending: true,
            page: 1,
            rowsPerPage: 0
        })

        const minv = ref(0.0)
        const maxv = ref(0.0)

        onMounted(async () => {
            const response = await api.get<IPredictionScoreType[]>('/predictions/score')

            let pos = 0
            let score = 0

            const standingsResponse = response.data.map((x, i) => {
                if (score !== x.total) {
                    pos = i + 1
                }
                score = x.total
                return { position: pos, ...x }
            })

            standings.value = standingsResponse
            const r = standings.value.map((x) => x.ratio)

            minv.value = r.reduce((v, x) => Math.min(v, x))
            maxv.value = r.reduce((v, x) => Math.max(v, x))

            isLoading.value = false
        })

        function ratioColor (val: number) {
            const step = (maxv.value - minv.value) / 5
            let v = minv.value + step
            if (val < v) {
                return 'red'
            }
            v += step
            if (val < v) {
                return 'red'
            }
            v += step
            if (val < v) {
                return 'info'
            }
            v += step
            if (val < v) {
                return 'green'
            }
            return 'green'
        }

        function ratioIcon (val: number) {
            const step = (maxv.value - minv.value) / 5
            let v = minv.value + step
            if (val < v) {
                return 'mdi-chevron-double-down'
            }
            v += step
            if (val < v) {
                return 'mdi-chevron-down'
            }
            v += step
            if (val < v) {
                return 'mdi-equal'
            }
            v += step
            if (val < v) {
                return 'mdi-chevron-up'
            }
            return 'mdi-chevron-double-up'
        }

        function ratioTitle (val: number) {
            return `${(Math.round(val * 1000) / 1000).toFixed(3)} %`
        }

        return {
            columns,
            isLoading,
            pagination,
            ratioColor,
            ratioIcon,
            ratioTitle,
            standings
        }
    }
})
