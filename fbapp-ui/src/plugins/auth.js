import authService from '../services/auth-service'

class AuthPlugin {
    state = 'authenticating'

    constructor () {
        this.events = {}

        authService.renewToken()
            .then((isAuthenticated) => {
                if (isAuthenticated) {
                    this.state = 'authenticated'
                    this.emit('authenticated')
                } else {
                    this.state = 'unauthenticated'
                    this.emit('unauthenticated')
                }
            })
            .catch((err) => {
                this.emit('unauthenticated')
                console.error(err)
            })
    }

    on (name, listener) {
        if (!this.events[name]) {
            this.events[name] = []
        }
        this.events[name].push(listener)
    }

    off (name, listenerToRemove) {
        if (this.events[name]) {
            this.events[name] = this.events[name].filter((listener) => listener !== listenerToRemove)
        }
    }

    emit (name, data) {
        const listeners = this.events[name]
        if (listeners) {
            listeners.forEach((callback) => callback(data))
        }
    }
}

export default ({ Vue }) => {
    Vue.prototype.$auth = new AuthPlugin()
}
