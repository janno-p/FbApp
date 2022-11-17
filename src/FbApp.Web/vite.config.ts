import { defineConfig } from 'vite'
import Elm from 'vite-plugin-elm'

export default defineConfig({
    plugins: [
        Elm()
    ],

    server: {
        hmr: {
            clientPort: 8090,
            protocol: 'wss'
        },
        strictPort: true
    }
})
