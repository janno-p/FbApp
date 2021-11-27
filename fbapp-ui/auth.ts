import { Log, User, UserManager } from 'oidc-client'

interface IAnonymousUser {
    kind: 'anonymous'
}

interface IAuthenticatedUser {
    kind: 'authenticated'
    user: User
}

type AuthState =
    | IAnonymousUser
    | IAuthenticatedUser

const NUMBER_OF_RENEW_RETRIES = 10

class AuthService {
    private readonly _manager: UserManager
    private _state?: AuthState

    get state() {
        return this._state
    }

    constructor() {
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
            this._state = { kind: 'authenticated', user }
        })

        this._manager.events.addUserUnloaded(() => {
            Log.info('user unloaded')
            this._state = undefined
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
            this._state = { kind: 'anonymous' }
        })

        this._manager.events.addUserSignedOut(() => {
            Log.info('user signed out')
            this.signOut().catch((err) => Log.info(err))
        })

        Log.logger = console
        Log.level = import.meta.env.DEV ? Log.DEBUG : Log.ERROR

        Log.info('start loading')

        void this.renewToken()
            .then((user) => {
                if (user === null) {
                    this._state = { kind: 'anonymous' }
                } else {
                    this._state = { kind: 'authenticated', user }
                }
            })
            .catch((err) => {
                Log.error(err)
                this._state = { kind: 'anonymous' }
            })
            .finally(() => {
                Log.info('loading done')
            })
    }

    private async signOutImpl() {
        this._state = { kind: 'anonymous' }
        const response = await this._manager.signoutRedirect()
        Log.info('signed out', response)
    }

    private async renewTokenWithRetries (attempt?: number): Promise<User | null> {
        try {
            return await this._manager.signinSilent()
        } catch (err) {
            const errObj = err as { error?: string, message?: string }
            const errorMessage = errObj.error ?? errObj.message ?? ''
            if (errorMessage === 'login_required') {
                Log.info('User not authenticated')
                return null
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

    private renewToken () {
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

export const authService = new AuthService()
