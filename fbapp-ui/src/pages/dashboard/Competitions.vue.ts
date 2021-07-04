import AddCompetition from 'components/dashboard/AddCompetition.vue'
import { defineComponent, ref } from 'vue'

interface ITableColumn<T> {
    name: string
    label: string
    field: keyof T | ((row: T) => string)
    required?: boolean
    align?: string
    sortable?: boolean
    sort?: (a: unknown, b: unknown, rowA: T, rowB: T) => number
    sortOrder?: string
    format?: (val: unknown, row: T) => unknown
    style?: string | ((row: T) => string)
    classes?: string | ((row: T) => string)
    headerStyle?: string
    headerClasses?: string
}

interface ITablePagination {
    sortBy?: string
    descending?: boolean
    page?: number
    rowsPerPage?: number
    rowsNumber?: number
}

interface ICompetition {
    id: string,
    description: string
}

export default defineComponent({
    name: 'Competitions',

    components: {
        AddCompetition
    },

    setup () {
        const columns: ITableColumn<ICompetition>[] = [
            {
                name: 'description',
                required: true,
                label: 'Kirjeldus',
                align: 'left',
                field: 'description',
                sortable: true
            }
        ]

        const isDataLoading = ref(false)

        const pagination: ITablePagination = {
            descending: false,
            page: 1,
            rowsPerPage: 0
        }

        const rows = ref<ICompetition[]>([])
        const selected = ref<ICompetition[]>([])

        function addCompetition () {
            isModalOpen.value = true
        }

        function competitionAdded (...args: unknown[]) {
            console.log(args)
        }

        const isModalOpen = ref(false)

        return {
            addCompetition,
            columns,
            competitionAdded,
            isDataLoading,
            isModalOpen,
            pagination,
            rows,
            selected
        }
    }
})
