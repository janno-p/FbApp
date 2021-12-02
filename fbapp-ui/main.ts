import { Elm } from './src/Main.elm'

import 'virtual:windi.css'
import 'virtual:windi-devtools'

import './styles/main.css'

import { authService } from './auth'

const app = Elm.Main.init({
    node: document.querySelector('#app')
})

authService.onAuthenticated((user) => {
    app.ports.authenticated.send(user)
})
