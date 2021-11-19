import { defineConfig } from 'vite'
import Elm from 'vite-plugin-elm'
import WindiCss from 'vite-plugin-windicss'

export default defineConfig({
    plugins: [
        Elm(),
        WindiCss()
    ]
})
