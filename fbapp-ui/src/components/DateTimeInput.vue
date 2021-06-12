<template>
    <q-input v-model="dateProxy" outlined :label="label">
        <template #prepend>
            <q-icon name="event" class="cursor-pointer">
                <q-popup-proxy transition-show="scale" transition-hide="scale">
                    <q-date v-model="dateProxy" mask="YYYY-MM-DD HH:mm">
                        <div class="row items-center justify-end">
                            <q-btn v-close-popup label="Close" color="primary" flat />
                        </div>
                    </q-date>
                </q-popup-proxy>
            </q-icon>
        </template>

        <template #append>
            <q-icon name="access_time" class="cursor-pointer">
                <q-popup-proxy transition-show="scale" transition-hide="scale">
                    <q-time v-model="dateProxy" mask="YYYY-MM-DD HH:mm" format24h>
                        <div class="row items-center justify-end">
                            <q-btn v-close-popup label="Close" color="primary" flat />
                        </div>
                    </q-time>
                </q-popup-proxy>
            </q-icon>
        </template>
    </q-input>
</template>

<script src="./DateTimeInput.vue.ts" />
