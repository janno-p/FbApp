<template>
    <div class="q-pa-lg">
        <q-inner-loading v-if="isLoading" :visible="true">
            <q-spinner-puff size="100px" color="primary" />
            <p class="q-mt-lg">Ennustuste hetkeseisu küsimine &hellip;</p>
        </q-inner-loading>
        <q-table
            v-else
            :data="standings"
            :columns="columns"
            row-key="name"
            hide-bottom
            :pagination.sync="pagination"
        >
            <q-tr slot="body" slot-scope="props" :props="props">
                <q-td v-for="col in props.cols" :key="col.name" :props="props">
                    <template v-if="col.name === 'total'">
                        {{ col.value[0] }}
                        <q-icon :name="ratioIcon(col.value[1])" :color="ratioColor(col.value[1])" :title="ratioTitle(col.value[1])" />
                    </template>
                    <template v-else>
                        {{ col.value }}
                    </template>
                </q-td>
            </q-tr>
        </q-table>
    </div>
</template>

<script>
import _ from "lodash"

export default {
    name: "PageViewScoreTable",

    data () {
        return {
            isLoading: true,
            standings: null,
            columns: [
                {
                    name: "pos",
                    required: true,
                    label: "",
                    align: "left",
                    field: "position",
                    sortable: true
                },
                {
                    name: "name",
                    required: true,
                    label: "Nimi",
                    align: "left",
                    field: "name",
                    sortable: true
                },
                {
                    name: "group_stage",
                    label: "Alagrupivoor",
                    align: "right",
                    field: (r) => r.points[0],
                    sortable: true
                },
                {
                    name: "round_of_16",
                    label: "Edasipääsejad",
                    align: "right",
                    field: (r) => r.points[1],
                    sortable: true
                },
                {
                    name: "quarter_finals",
                    label: "Veerandfinalistid",
                    align: "right",
                    field: (r) => r.points[2],
                    sortable: true
                },
                {
                    name: "semi_finals",
                    label: "Poolfinalistid",
                    align: "right",
                    field: (r) => r.points[3],
                    sortable: true
                },
                {
                    name: "final",
                    label: "Finalistid",
                    align: "right",
                    field: (r) => r.points[4],
                    sortable: true
                },
                {
                    name: "winner",
                    label: "Võitja",
                    align: "right",
                    field: (r) => r.points[5],
                    sortable: true
                },
                {
                    name: "total",
                    label: "Kokku",
                    align: "right",
                    field: (r) => [r.total, r.ratio],
                    sortable: true
                }
            ],
            pagination: {
                sortBy: "total",
                descending: true,
                page: 1,
                rowsPerPage: 0
            },
            minv: 0.0,
            maxv: 0.0
        }
    },

    async mounted () {
        const response = await this.$axios.get("/predictions/score")
        let pos = 0
        let score = 0
        const standings = _(response.data).map((x, i) => {
            if (score !== x.total) {
                pos = i + 1
            }
            score = x.total
            return { position: pos, ...x }
        }).value()
        this.$set(this, "standings", standings)
        const r = _(this.standings).map((x) => x.ratio)
        this.minv = r.min()
        this.maxv = r.max()
        this.isLoading = false
    },

    methods: {
        ratioColor (val) {
            const step = (this.maxv - this.minv) / 5
            let v = this.minv + step
            if (val < v) {
                return "red"
            }
            v += step
            if (val < v) {
                return "red"
            }
            v += step
            if (val < v) {
                return "info"
            }
            v += step
            if (val < v) {
                return "green"
            }
            return "green"
        },

        ratioIcon (val) {
            const step = (this.maxv - this.minv) / 5
            let v = this.minv + step
            if (val < v) {
                return "mdi-chevron-double-down"
            }
            v += step
            if (val < v) {
                return "mdi-chevron-down"
            }
            v += step
            if (val < v) {
                return "mdi-equal"
            }
            v += step
            if (val < v) {
                return "mdi-chevron-up"
            }
            return "mdi-chevron-double-up"
        },

        ratioTitle (val) {
            return `${(Math.round(val * 1000) / 1000).toFixed(3)} %`
        }
    }
}
</script>
