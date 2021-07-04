import { computed, defineComponent } from 'vue'

export default defineComponent({
    name: 'DateTimeInput',

    props: {
        modelValue: {
            required: true,
            type: String
        },

        label: String
    },

    emits: [
        'update:model-value'
    ],

    setup (props, { emit }) {
        const dateProxy = computed({
            get () {
                return props.modelValue
            },
            set (value: string) {
                emit('update:model-value', value)
            }
        })

        return { dateProxy }
    }
})
