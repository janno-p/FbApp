import DateTimeInput from 'components/DateTimeInput.vue'
import { api } from 'src/boot/axios'
import { computed, defineComponent, ref, watch } from 'vue'

export default defineComponent({
    name: 'AddCompetition',

    components: {
        DateTimeInput
    },

    props: {
        modelValue: {
            type: Boolean,
            default: false
        }
    },

    emits: [
        'competition-added',
        'update:model-value'
    ],

    setup (props, { emit }) {
        const initialYear = new Date().getFullYear()
        const seasonOptions = [...Array(5).keys()].map((v) => initialYear - v).map((value) => value.toString())

        const modelValueProxy = computed({
            get () {
                return props.modelValue
            },
            set (value: boolean) {
                emit('update:model-value', value)
            }
        })

        const description = ref('')
        const season = ref(initialYear)
        const dataSource = ref<string>()
        const date = ref('2021-06-10 15:35')

        const dataSourceOptions = ref<Array<{ label: string, value: unknown }>>([])
        const isDataSourceLoading = ref(false)
        const isSaving = ref(false)

        async function saveCompetition () {
            isSaving.value = true
            try {
                const payload = {
                    id: '',
                    description: description.value,
                    externalId: dataSource.value,
                    date: date.value
                }

                const response = await api.post<{ resourceId: string }>('/api/competitions', payload)
                payload.id = response.data.resourceId

                emit('competition-added', payload)
                emit('update:model-value', false)
            } finally {
                isSaving.value = false
            }
        }

        watch(season, async (year) => {
            isDataSourceLoading.value = true
            dataSource.value = undefined
            dataSourceOptions.value = []
            if (year) {
                try {
                    const response = await api.get<Array<{ label: string, value: unknown }>>(`/api/competitions/${year}`)
                    dataSourceOptions.value = response.data
                } catch (err) {
                    console.error(err)
                }
            }
            isDataSourceLoading.value = false
        }, { immediate: true })

        return {
            description,
            season,
            dataSource,
            date,
            seasonOptions,
            dataSourceOptions,
            isDataSourceLoading,
            isSaving,
            modelValueProxy,
            saveCompetition
        }
    }
})
