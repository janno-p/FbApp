const { Elm } = require('./src/Main.elm')

Elm.Main.init({
    node: document.getElementById('app'),
    flags: {}
})