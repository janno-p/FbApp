import { Elm } from './src/Main.elm'

import 'virtual:windi.css'
import 'virtual:windi-devtools'

import './styles/main.css'

import { authService } from './auth'

console.log(authService.state)

const root = document.querySelector('#app')
Elm.Main.init({ node: root })
