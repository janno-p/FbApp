import useAuthentication from 'src/hooks/authentication'
import { RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
    {
        path: '/',
        component: () => import('layouts/MainLayout.vue'),
        children: [
            {
                path: '',
                component: {},
                beforeEnter: (_to, _from, next) => {
                    const { competitionStatus, isSignedIn, predictions } = useAuthentication()
                    if (isSignedIn.value && predictions.value) {
                        next('/view-predictions')
                    } else if (competitionStatus.value === 'accept-predictions') {
                        next('/add-predictions')
                    } else {
                        next('/competition/fixtures')
                    }
                }
            },
            {
                path: 'view-predictions',
                component: () => import('pages/ViewPredictions.vue'),
                beforeEnter: (_to, _from, next) => {
                    const { isSignedIn, predictions } = useAuthentication()
                    if (isSignedIn.value && predictions.value) {
                        next()
                    } else {
                        next('/competition/fixtures')
                    }
                }
            },
            {
                path: 'add-predictions',
                component: () => import('pages/AddPredictions.vue'),
                beforeEnter: (_to, _from, next) => {
                    const { competitionStatus } = useAuthentication()
                    if (competitionStatus.value === 'accept-predictions') {
                        next()
                    } else {
                        next('/competition/fixtures')
                    }
                }
            },
            {
                path: 'competition/',
                component: () => import('layouts/CompetitionLayout.vue'),
                children: [
                    {
                        name: 'timely-fixture',
                        path: 'fixtures',
                        component: () => import('pages/ViewFixture.vue')
                    },
                    {
                        name: 'fixture',
                        path: 'fixtures/:fixtureId',
                        component: () => import('pages/ViewFixture.vue')
                    },
                    {
                        path: 'score-table',
                        component: () => import('pages/ViewScoretable.vue')
                    }
                ]
            },
            {
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
                path: '',
                component: () => import('pages/dashboard/Index.vue')
            },

            {
                path: 'competitions',
                component: () => import('pages/dashboard/Competitions.vue')
            },

            {
                path: 'leagues',
                component: () => import('pages/dashboard/Leagues.vue')
            }
        ]
    },

    {
        path: '/:catchAll(.*)*',
        component: () => import('pages/Error404.vue')
    }
]

export default routes
