<template>
    <q-page class="q-pa-lg">
        <q-inner-loading v-if="isInitializing" :visible="true">
            <q-spinner-puff size="100px" color="primary" />
            <p class="q-mt-lg">Võistluste hetkeseisu küsimine &hellip;</p>
        </q-inner-loading>
        <template v-else>
            <h4>Varsti &hellip;</h4>
        </template>
    </q-page>
</template>

<script>
export default {
    name: "AppCompetitionView",

    data () {
        return {
            isInitializing: true
        }
    },

    mounted () {
        this.isInitializing = false
    }
}
</script>
