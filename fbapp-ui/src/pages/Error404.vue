<template>
    <div class="fullscreen bg-blue text-white text-center q-pa-md flex flex-center">
        <div>
            <div style="font-size: 30vh">
                404
            </div>

            <div
                class="text-h2"
                style="opacity:.4"
            >
                Vabandust, aga siin pole midagi &hellip;
            </div>

            <q-btn
                class="q-mt-xl"
                color="white"
                text-color="blue"
                unelevated
                to="/"
                label="Go Home"
                no-caps
            />
        </div>
    </div>
</template>

<script>
import Error404 from './Error404.vue.ts'
export default Error404
</script>
