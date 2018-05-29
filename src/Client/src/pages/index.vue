<template>
    <q-page class="q-pa-lg">
        <q-stepper ref="stepper" vertical>
            <q-step default title="Ennustusmäng" subtitle="Here we go">
                <p>Ajavahemikus 14. juunist 15. juulini toimuvad Venemaal 2018. aasta jalgpalli
                    maailmameistrivõistlused. Lisaks rahvusmeeskondade mõõduvõtmistele pakub antud veebileht
                    omavahelist võistlusmomenti ka tugitoolisportlastele tulemuste ennustamise näol.</p>

                <p>Oma eelistusi saad valida ja muuta kuni avamänguni 14. juunil kell 18:00. Pärast seda on
                    võimalik sama veebilehe vahendusel jälgida, kuidas tegelikud tulemused kujunevad ning kui täpselt
                    need Sinu või teiste ennustustega kokku langevad.</p>

                <p>Auhinnaks lühiajaline au ja kuulsus.</p>

                <q-btn color="positive" @click="$refs.stepper.next()" label="Tee oma ennustused »" />
            </q-step>

            <q-step title="Second Step">
                Head Aega!
            </q-step>
        </q-stepper>
    </q-page>
</template>

<script>
export default {
    name: "PageIndex"
}
</script>
