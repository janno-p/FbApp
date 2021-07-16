﻿import { Todo, Meta } from 'components/models'
import ExampleComponent from 'components/CompositionComponent.vue'
import CompetitionView from 'components/CompetitionView.vue'
import { defineComponent, ref } from 'vue'

export default defineComponent({
    name: 'PageIndex',

    components: {
        CompetitionView,
        ExampleComponent
    },

    setup () {
        const todos = ref<Todo[]>([
            {
                id: 1,
                content: 'ct1'
            },
            {
                id: 2,
                content: 'ct2'
            },
            {
                id: 3,
                content: 'ct3'
            },
            {
                id: 4,
                content: 'ct4'
            },
            {
                id: 5,
                content: 'ct5'
            }
        ])

        const meta = ref<Meta>({
            totalCount: 1200
        })

        return { todos, meta }
    }
})
