import { Log, UserManager } from 'oidc-client'
import { boot } from 'quasar/wrappers'
import { readonly, ref } from 'vue'

declare module '@vue/runtime-core' {
    interface ComponentCustomProperties {
        $auth: AuthService
    }
}

type AuthenticationState =
    | 'authenticating'
    | 'authenticated'
    | 'anonymous'

const NUMBER_OF_RENEW_RETRIES = 10

class AuthService {
    private readonly _manager: UserManager
    private _state = ref<AuthenticationState>('authenticating')

    get state() {
        return readonly(this._state)
    }

    constructor () {
        this._manager = new UserManager({
            redirect_uri: 'https://localhost:8090/oidc.html',
            silent_redirect_uri: 'https://localhost:8090/oidc.html',
            response_type: 'code',
            post_logout_redirect_uri: 'https://localhost:8090/',
            accessTokenExpiringNotificationTime: 60,
            automaticSilentRenew: true,
            filterProtocolClaims: true,
            loadUserInfo: true,
            client_id: 'fbapp-ui-client',
            authority: 'https://localhost:8090/',
            scope: 'openid profile email roles'
        })

        this._manager.events.addUserLoaded((user) => {
            Log.info('new user loaded: ', user)
            Log.info('access token: ', user.access_token)
            this._state.value = 'authenticated'
        })

        this._manager.events.addUserUnloaded(() => {
            Log.info('user unloaded')
            this._state.value = 'authenticating'
        })

        this._manager.events.addAccessTokenExpiring(() => {
            Log.info('access token expiring')
        })

        this._manager.events.addAccessTokenExpired(() => {
            Log.info('access token expired, trying to renew access token')
            this.renewTokenWithRetries()
                .then((isAuthenticated) => {
                    if (!isAuthenticated) {
                        void this.signOut()
                    }
                })
                .catch((err) => Log.info(err))
        })

        this._manager.events.addSilentRenewError((error) => {
            Log.error('silent renew error: ', error)
            this._state.value = 'anonymous'
        })

        this._manager.events.addUserSignedOut(() => {
            Log.info('user signed out')
            this.signOut().catch((err) => Log.info(err))
        })

        Log.logger = console
        Log.level = process.env.NODE_ENV === 'development' ? Log.INFO : Log.ERROR

        void this.renewToken()
    }

    private async signOutImpl () {
        this._state.value = 'anonymous'
        const resp = await this._manager.signoutRedirect()
        Log.info('signed out', resp)
    }

    private async renewTokenWithRetries (attempt?: number): Promise<boolean> {
        try {
            const user = await this._manager.signinSilent()
            if (user === null) {
                return false
            } else {
                return true
            }
        } catch (err) {
            const errObj = err as { error?: string, message?: string }
            const errorMessage = errObj.error ?? errObj.message ?? ''
            if (errorMessage === 'login_required') {
                Log.info('User not authenticated')
                return false
            }
            if (errorMessage === 'Frame window timed out') {
                attempt = (attempt || 0) + 1
                if (attempt < NUMBER_OF_RENEW_RETRIES) {
                    Log.info(`Token renewal timed out (retry ${attempt})`)
                    return await this.renewTokenWithRetries(attempt)
                } else {
                    Log.info('Token renewal timed out (giving up)')
                }
            }
            throw err
        }
    }

    renewToken () {
        return this.renewTokenWithRetries()
    }

    signOut (): Promise<void> {
        try {
            return this.signOutImpl()
        } catch (err) {
            Log.info(err)
            return Promise.resolve()
        }
    }
}

const authService = new AuthService()

export default boot(({ app }) => {
    app.config.globalProperties.$auth = authService
})
