// Configuration for your app

module.exports = function (ctx) {
    return {
        // app plugins (/src/plugins)
        plugins: [
            "auth2",
            "axios"
        ],
        css: [
            'app.styl'
        ],
        extras: [
            'roboto-font',
            'material-icons',
            'mdi',
            'fontawesome'
        ],
        supportIE: true,
        build: {
            scopeHoisting: true,
            vueRouterMode: 'history',
            // vueCompiler: true,
            // gzip: true,
            // analyze: true,
            // extractCSS: false,
            extendWebpack (cfg) {
                cfg.module.rules.push({
                    enforce: 'pre',
                    test: /\.(js|vue)$/,
                    loader: 'eslint-loader',
                    exclude: /(node_modules|quasar)/
                })
            },
            env: {
                API: JSON.stringify("/api")
            },
            distDir: "../FbApp.Server/wwwroot"
        },
        devServer: {
            proxy: {
                "/api": {
                    target: "https://localhost:5001",
                    changeOrigin: true,
                    secure: false
                }
            },
            https: true,
            // port: 8080,
            open: true, // opens browser window automatically
            public: 'localhost:8090'
        },
        framework: ctx.dev
            ? "all" // includes everything; for dev only!
            : {
                components: [
                    "QBtn",
                    "QDatetime",
                    "QField",
                    "QIcon",
                    "QInnerLoading",
                    "QInput",
                    "QItem",
                    "QItemMain",
                    "QItemSeparator",
                    "QItemSide",
                    "QItemTile",
                    "QLayout",
                    "QLayoutHeader",
                    "QList",
                    "QListHeader",
                    "QModal",
                    "QLayoutDrawer",
                    "QModalLayout",
                    "QPage",
                    "QPageContainer",
                    "QPageSticky",
                    "QRating",
                    "QRouteTab",
                    "QSelect",
                    "QSpinnerPie",
                    "QSpinnerPuff",
                    "QStep",
                    "QStepper",
                    "QTable",
                    "QTabs",
                    "QTd",
                    "QToolbar",
                    "QToolbarTitle",
                    "QTr"
                ],
                directives: [
                    "Ripple"
                ],
                plugins: [
                    "Notify"
                ],
                iconSet: ctx.theme.mat ? 'material-icons' : 'ionicons'
            },
        animations: ctx.dev
            ? 'all' // includes all animations
            : [
            ],
        pwa: {
            // workboxPluginMode: 'InjectManifest',
            // workboxOptions: {},
            manifest: {
                // name: 'Quasar App',
                // short_name: 'Quasar-PWA',
                // description: 'Best PWA App in town!',
                display: 'standalone',
                orientation: 'portrait',
                background_color: '#ffffff',
                theme_color: '#027be3',
                icons: [
                    {
                        'src': 'statics/icons/icon-128x128.png',
                        'sizes': '128x128',
                        'type': 'image/png'
                    },
                    {
                        'src': 'statics/icons/icon-192x192.png',
                        'sizes': '192x192',
                        'type': 'image/png'
                    },
                    {
                        'src': 'statics/icons/icon-256x256.png',
                        'sizes': '256x256',
                        'type': 'image/png'
                    },
                    {
                        'src': 'statics/icons/icon-384x384.png',
                        'sizes': '384x384',
                        'type': 'image/png'
                    },
                    {
                        'src': 'statics/icons/icon-512x512.png',
                        'sizes': '512x512',
                        'type': 'image/png'
                    }
                ]
            }
        },
        cordova: {
            // id: 'org.cordova.quasar.app'
        },
        electron: {
            // bundler: 'builder', // or 'packager'
            extendWebpack (cfg) {
                // do something with Electron process Webpack cfg
            },
            packager: {
                // https://github.com/electron-userland/electron-packager/blob/master/docs/api.md#options

                // OS X / Mac App Store
                // appBundleId: '',
                // appCategoryType: '',
                // osxSign: '',
                // protocol: 'myapp://path',

                // Window only
                // win32metadata: { ... }
            },
            builder: {
                // https://www.electron.build/configuration/configuration

                // appId: 'quasar-app'
            }
        }
    }
};
