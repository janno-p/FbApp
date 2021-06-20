import useAuthentication from 'src/hooks/authentication'
import { computed, defineComponent } from 'vue'

export default defineComponent({
    name: 'ViewPredictions',

    setup () {
        const { isLoadingPredictions, predictions } = useAuthentication()

        function findTeam (id?: number) {
            const teams = predictions.value?.teams ?? {}
            return id ? teams[id] : undefined
        }

        const fixtures = computed(() => {
            return (predictions.value?.fixtures ?? []).map((f) => ({
                homeTeam: findTeam(f.homeTeam),
                awayTeam: findTeam(f.awayTeam),
                fixture: f.fixture,
                result: f.result
            }))
        })

        const roundOf16 = computed(() => (predictions.value?.roundOf16 || []).map((id) => findTeam(id)))
        const roundOf8 = computed(() => (predictions.value?.roundOf8 || []).map((id) => findTeam(id)))
        const roundOf4 = computed(() => (predictions.value?.roundOf4 || []).map((id) => findTeam(id)))
        const roundOf2 = computed(() => (predictions.value?.roundOf2 || []).map((id) => findTeam(id)))
        const winner = computed(() => findTeam(predictions.value?.winner))

        return {
            fixtures,
            isLoadingPredictions,
            predictions,
            roundOf16,
            roundOf8,
            roundOf4,
            roundOf2,
            winner
        }
    }
})
