<template>
    <transition appear enter-active-class="animated fadeIn" leave-active-class="animated fadeOut" mode="out-in">
        <q-inner-loading v-if="isLoading" key="loading" :showing="true">
            <q-spinner-puff size="100px" color="primary" />
            <p class="q-mt-lg">Aktiivse võistluse küsimine &hellip;</p>
        </q-inner-loading>
        <slot v-else-if="isActiveCompetition" key="content" />
        <div v-else class="q-pa-md q-gutter-sm">
            <q-banner key="no-competition" class="bg-grey-3">
                <template #avatar>
                    <q-icon name="mdi-emoticon-sad-outline" color="primary" />
                </template>
                Hetkel ei ole ühtegi käimasolevat võistlust.
            </q-banner>
        </div>
    </transition>
</template>

<script src="./CompetitionView.vue.ts" />
