import { Elm } from '../src/Main.elm'

import 'virtual:windi.css'
import 'virtual:windi-devtools'

import '@mdi/font/css/materialdesignicons.min.css'
import '../styles/main.css'

function rememberedBytes() {
    const bytes = localStorage.getItem('bytes')
    return bytes ? bytes.split(',').map(x => parseInt(x, 10)) : null
}

const app = Elm.Main.init({
    node: document.getElementById('app'),
    flags: rememberedBytes()
})

app.ports.generateRandomBytes.subscribe(numberOfBytes => {
    const buffer = new Uint8Array(numberOfBytes)
    crypto.getRandomValues(buffer)
    const bytes = Array.from(buffer)
    localStorage.setItem('bytes', bytes.join(','))
    app.ports.randomBytes.send(bytes)
})
