<template>
    <q-header elevated>
        <q-toolbar>
            <q-btn type="a" flat stretch aria-label="Menu" @click.prevent="goHome">
                <div class="row items-center no-wrap">
                    <q-icon left name="mdi-soccer" size="lg" />
                    <div class="text-left">
                        <h5 class="q-py-none q-my-none">Ennustusmäng</h5>
                        <div class="text-caption">{{ competitionName }}</div>
                    </div>
                </div>
            </q-btn>

            <q-space />

            <q-btn icon="mdi-playlist-check" flat stretch title="Muudatuste logi" :to="{ name: 'changelog' }" />

            <q-btn v-if="isAnonymousUser" type="a" icon="mdi-google" flat stretch title="Logi sisse Google kontoga" href="/connect/google" />

            <template v-else>
                <div class="q-px-md row items-center no-wrap">
                    <q-icon name="mdi-account-tie" size="1.715rem" />
                    <div class="q-pl-sm text-weight-medium">{{ userName }}</div>
                </div>
                <q-btn icon="mdi-cog-outline" flat stretch title="Ava kontrollpaneel" />
                <q-btn icon="mdi-logout" flat stretch title="Logi välja" @click="logout" />
            </template>
        </q-toolbar>
    </q-header>
</template>

<script src="./SiteToolbar.vue.ts" />
