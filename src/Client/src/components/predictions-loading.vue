<template>
    <q-page class="q-pa-lg">
        <q-inner-loading :visible="true">
            <q-spinner-puff size="100px" color="primary" />
            <p class="q-mt-lg">Varasemate ennustuste kontrollimine &hellip;</p>
        </q-inner-loading>
    </q-page>
</template>

<script>
export default {
    name: "AppPredictionsLoading"
}
</script>
