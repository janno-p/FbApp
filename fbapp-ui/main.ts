import { Elm } from './src/Main.elm'

import 'virtual:windi.css'
import 'virtual:windi-devtools'

import './styles/main.css'

const root = document.querySelector('#app')
const app = Elm.Main.init({ node: root })
