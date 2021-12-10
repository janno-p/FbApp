import { Elm } from './src/Main.elm'

import 'virtual:windi.css'
import 'virtual:windi-devtools'

import './styles/main.css'

import { authService } from './auth'

const app = Elm.Main.init({
    node: document.querySelector('#app')
})

app.ports.signOut.subscribe(() => authService.signOut())

authService.onAuthenticated((user) => {
    app.ports.onStoreChange.send(user)
})
