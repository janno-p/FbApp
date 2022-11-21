import { Elm } from '../src/Main.elm'

import '@mdi/font/css/materialdesignicons.min.css'
import '../styles/main.css'

function rememberedBytes() {
    const bytes = localStorage.getItem('bytes')
    return bytes ? bytes.split(',').map(x => parseInt(x, 10)) : null
}

function rememberedPath() {
    return localStorage.getItem('path') ?? '/'
}

const app = Elm.Main.init({
    node: document.getElementById('app'),
    flags: [rememberedBytes(), rememberedPath()]
})

app.ports.generateRandomBytes.subscribe(([numberOfBytes, path]) => {
    const buffer = new Uint8Array(numberOfBytes)
    crypto.getRandomValues(buffer)
    const bytes = Array.from(buffer)
    localStorage.setItem('bytes', bytes.join(','))
    localStorage.setItem('path', path)
    app.ports.randomBytes.send([bytes, path])
})
