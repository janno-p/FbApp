import useActiveCompetition from 'src/use/active-competition'
import { defineComponent } from 'vue'

export default defineComponent({
    name: 'CompetitionView',

    setup() {
        const { isActiveCompetition, isLoading } = useActiveCompetition()
        return { isActiveCompetition, isLoading }
    }
})
