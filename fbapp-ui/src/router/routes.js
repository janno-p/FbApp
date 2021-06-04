import store from '../store'

export default [
    {
        path: '/',
        component: () => import('layouts/default'),
        children: [
            {
                path: '',
                component: {},
                beforeEnter: (to, from, next) => {
                    if (store.state.isSignedIn && store.state.predictions) {
                        next('/view-predictions')
                    } else if (store.state.competitionStatus === 'accept-predictions') {
                        next('/add-predictions')
                    } else {
                        next('/competition/fixtures')
                    }
                }
            },
            {
                path: 'view-predictions',
                component: () => import('pages/view-predictions'),
                beforeEnter: (to, from, next) => {
                    if (store.state.isSignedIn && store.state.predictions) {
                        next()
                    } else {
                        next('/competition/fixtures')
                    }
                }
            },
            {
                path: 'add-predictions',
                component: () => import('pages/add-predictions'),
                beforeEnter: (to, from, next) => {
                    if (store.state.competitionStatus === 'accept-predictions') {
                        next()
                    } else {
                        next('/competition/fixtures')
                    }
                }
            },
            {
                path: 'competition/',
                component: () => import('layouts/competition'),
                children: [
                    {
                        path: 'fixtures',
                        component: () => import('pages/view-fixture')
                    },
                    {
                        path: 'fixtures/:fixture_id',
                        component: () => import('pages/view-fixture')
                    },
                    {
                        path: 'score-table',
                        component: () => import('pages/view-score-table')
                    }
                ]
            },
            {
                path: 'changelog',
                component: () => import('pages/changelog')
            }
        ]
    },

    {
        path: '/dashboard',
        component: () => import('layouts/dashboard'),
        children: [
            {
                path: '',
                component: () => import('pages/dashboard/index')
            },

            {
                path: 'competitions',
                component: () => import('pages/dashboard/competitions')
            },

            {
                path: 'leagues',
                component: () => import('pages/dashboard/leagues')
            }
        ]
    },

    // Always leave this as last one:
    {
        path: '*',
        component: () => import('pages/404')
    }
]
