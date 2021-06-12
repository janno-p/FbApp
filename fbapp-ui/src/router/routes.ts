import { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
    {
        path: '/',
        component: () => import('layouts/MainLayout.vue'),
        children: [
            {
                name: 'home',
                path: '',
                component: () => import('pages/Index.vue')
            },
            {
                name: 'changelog',
                path: 'changelog',
                component: () => import('pages/Changelog.vue')
            }
        ]
    },

    {
        path: '/dashboard',
        component: () => import('layouts/DashboardLayout.vue'),
        children: [
            {
                name: 'dashboard',
                path: '',
                component: () => import('src/pages/dashboard/Dashboard.vue')
            },
            {
                name: 'competitions',
                path: 'competitions',
                component: () => import('src/pages/dashboard/Competitions.vue')
            }
        ],
        beforeEnter(to, from, next) {
            // if has admin role
            return next()
            // else
            // return next('/')
        }
    },

    // Always leave this as last one,
    // but you can also remove it
    {
        path: '/:catchAll(.*)*',
        component: () => import('pages/Error404.vue')
    }
]

export default routes
