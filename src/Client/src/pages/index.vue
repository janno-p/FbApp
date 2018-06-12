<template>
    <component :is="activeComponent" :predictions="predictions" />
</template>

<script>
import AddPredictions from "../components/add-predictions"
import MyPredictions from "../components/my-predictions"
import PredictionsLoading from "../components/predictions-loading"
import { mapState } from "vuex"

export default {
    name: "PageIndex",

    components: {
        AddPredictions,
        MyPredictions,
        PredictionsLoading
    },

    computed: {
        activeComponent () {
            if (this.isLoading) {
                return "predictions-loading"
            } else if (this.isSignedIn && this.predictions) {
                return "my-predictions"
            } else {
                return "add-predictions"
            }
        },

        ...mapState([
            "isSignedIn"
        ])
    },

    data () {
        return {
            isLoading: true,
            predictions: null
        }
    },

    methods: {
        async loadPredictions () {
            this.isLoading = true
            try {
                const response = await this.$axios.get("/predict/current")
                this.$set(this, "predictions", response.data)
            } finally {
                this.isLoading = false
            }
        }
    },

    mounted () {
        if (this.isSignedIn) {
            this.$nextTick(async () => {
                await this.loadPredictions()
            })
        } else {
            this.isLoading = false
        }
    },

    watch: {
        async isSignedIn (value) {
            if (value) {
                await this.loadPredictions()
            } else {
                this.$set(this, "predictions", null)
            }
        }
    }
}
</script>
