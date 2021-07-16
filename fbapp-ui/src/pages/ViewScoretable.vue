<template>
    <div class="q-pa-lg">
        <q-inner-loading
            v-if="isLoading"
            :visible="true"
        >
            <q-spinner-puff
                size="100px"
                color="primary"
            />
            <p class="q-mt-lg">
                Ennustuste hetkeseisu küsimine &hellip;
            </p>
        </q-inner-loading>
        <q-table
            v-else
            v-model:pagination="pagination"
            :rows="standings"
            :columns="columns"
            row-key="name"
            hide-bottom
        >
            <template #body="props">
                <q-tr :props="props">
                    <q-td
                        v-for="col in props.cols"
                        :key="col.name"
                        :props="props"
                    >
                        <template v-if="col.name === 'ratio'">
                            <q-icon
                                :name="ratioIcon(col.value)"
                                :color="ratioColor(col.value)"
                                :title="ratioTitle(col.value)"
                            />
                        </template>
                        <template v-else>
                            {{ col.value }}
                        </template>
                    </q-td>
                </q-tr>
            </template>
        </q-table>
    </div>
</template>

<script>
import ViewScoretable from './ViewScoretable.vue.ts'
export default ViewScoretable
</script>
