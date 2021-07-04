import { computed, readonly, ref } from 'vue'
import { api } from 'boot/axios'

interface ActiveCompetitionDto {
    id: string
    name: string
    status: string
}

const isLoading = ref(true)

const competition = ref<ActiveCompetitionDto>()

api.get<ActiveCompetitionDto>('/api/competitions', { validateStatus: (status) => status === 200 || status === 404 })
    .then((response) => {
        if (response.status === 200) {
            competition.value = response.data
        }
    })
    .catch((err) => {
        console.error(err)
    })
    .finally(() => {
        isLoading.value = false
    })

const isActiveCompetition = computed(() => !isLoading.value && competition.value !== undefined)

export default function useActiveCompetition () {
    return {
        competition: readonly(competition),
        isActiveCompetition,
        isLoading: readonly(isLoading)
    }
}
