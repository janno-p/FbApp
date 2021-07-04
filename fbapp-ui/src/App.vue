<template>
    <router-view v-if="isReady" />
    <p v-else>
        Tegelen, tegelen ...
    </p>
</template>

<script>
import App from './App.vue.ts'
export default App
</script>
