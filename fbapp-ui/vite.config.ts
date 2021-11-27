import { resolve } from 'path'
import { defineConfig } from 'vite'
import Elm from 'vite-plugin-elm'
import WindiCss from 'vite-plugin-windicss'

export default defineConfig({
    build: {
        rollupOptions: {
            input: {
                main: resolve(__dirname, './index.html'),
                oidc: resolve(__dirname, './oidc.html')
            }
        }
    },

    plugins: [
        Elm(),
        WindiCss()
    ],

    server: {
        hmr: {
            protocol: 'ws'
        },
        strictPort: true
    }
})
