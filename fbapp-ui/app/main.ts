import { Elm } from '../src/Main.elm'

import 'virtual:windi.css'
import 'virtual:windi-devtools'

import '@mdi/font/css/materialdesignicons.min.css'
import '../styles/main.css'

import { authService } from './auth'

authService.getUser()
    .then((flags) => {
        const app = Elm.Main.init({
            node: document.querySelector('#app'),
            flags
        })

        app.ports.signOut.subscribe(() => authService.signOut())

        authService.onAuthenticated((user) => {
            app.ports.onStoreChange.send(user)
        })
    })
