export default [
    {
        path: "/",
        component: () => import("layouts/default"),
        children: [
            {
                path: "",
                component: () => import("pages/index")
            },
            {
                path: "changelog",
                component: () => import("pages/changelog")
            }
        ]
    },

    {
        path: "/dashboard",
        component: () => import("layouts/dashboard"),
        children: [
            {
                path: "",
                component: () => import("pages/dashboard/index")
            },

            {
                path: "competitions",
                component: () => import("pages/dashboard/competitions")
            }
        ]
    },

    // Always leave this as last one:
    {
        path: "*",
        component: () => import("pages/404")
    }
]
