<template>
    <component
        :is="activeComponent"
        :predictions="predictions"
        @before-prediction-added="isPredictionAdded = true"
        @prediction-added="predictionAdded"
        @prediction-exists="loadPredictions">
    </component>
</template>

<script>
import AddPredictions from "../components/add-predictions"
import MyPredictions from "../components/my-predictions"
import PredictionsLoading from "../components/predictions-loading"
import CompetitionView from "../components/competition-view"
import { mapState } from "vuex"

export default {
    name: "PageIndex",

    components: {
        AddPredictions,
        MyPredictions,
        PredictionsLoading,
        CompetitionView
    },

    computed: {
        activeComponent () {
            if (this.isLoading) {
                return "predictions-loading"
            } else if (this.isSignedIn && this.predictions) {
                return "my-predictions"
            } else if (this.competitionStatus === "accept-predictions") {
                return "add-predictions"
            } else {
                return "competition-view"
            }
        },

        ...mapState([
            "isSignedIn"
        ])
    },

    data () {
        return {
            isPredictionAdded: false,
            isLoading: true,
            predictions: null,
            competitionStatus: "accept-predictions"
        }
    },

    methods: {
        async loadPredictions () {
            if (this.predictions) {
                return
            }
            this.isLoading = true
            try {
                const response = await this.$axios.get("/predict/current")
                this.$set(this, "predictions", response.data)
            } finally {
                this.isLoading = false
            }
        },

        predictionAdded (prediction) {
            this.isPredictionAdded = false
            this.$set(this, "predictions", prediction)
        }
    },

    mounted () {
        if (this.isSignedIn) {
            this.$nextTick(async () => {
                await this.loadPredictions()
            })
        } else {
            this.$nextTick(async () => {
                try {
                    const response = await this.$axios.get("/predict/status")
                    this.competitionStatus = response.data
                } finally {
                    this.isLoading = false
                }
            })
        }
    },

    watch: {
        async isSignedIn (value) {
            if (!value) {
                this.$set(this, "predictions", null)
            } else if (!this.isPredictionAdded) {
                await this.loadPredictions()
            }
        }
    }
}
</script>
