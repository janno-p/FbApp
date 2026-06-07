import { defineConfig } from 'vite'
import elmPlugin from 'vite-plugin-elm'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
    plugins: [
        elmPlugin(),
        tailwindcss()
    ],
    server: {
        hmr: {
            clientPort: 8090,
            protocol: 'wss'
        },
        strictPort: true
    }
})
