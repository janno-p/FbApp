<template>
    <div>
        <p>{{ title }}</p>
        <ul>
            <li v-for="todo in todos" :key="todo.id" @click="increment">
                {{ todo.id }} - {{ todo.content }}
            </li>
        </ul>
        <p>Count: {{ todoCount }} / {{ meta.totalCount }}</p>
        <p>Active: {{ active ? 'yes' : 'no' }}</p>
        <p>Clicks on todos: {{ clickCount }}</p>
    </div>
</template>

<script src="./CompositionComponent.vue.ts" />
