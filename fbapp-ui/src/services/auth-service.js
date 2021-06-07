import { Log, UserManager } from 'oidc-client'

const NUMBER_OF_RENEW_RETRIES = 10

class AuthService {
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
            // setAuthenticated(user)
        })

        this._manager.events.addUserUnloaded(() => {
            Log.info('user unloaded')
            // setAuthenticating()
        })

        this._manager.events.addAccessTokenExpiring(() => {
            Log.info('access token expiring')
        })

        this._manager.events.addAccessTokenExpired(async () => {
            Log.info('access token expired')
            try {
                Log.info('trying to renew access token')
                if (!await this.renewTokenWithRetries()) {
                    await this.signOut()
                }
            } catch (err) {
                Log.info(err)
            }
        })

        this._manager.events.addSilentRenewError((error) => {
            Log.error('silent renew error: ', error)
            // setUnauthenticated()
        })

        this._manager.events.addUserSignedOut(async () => {
            Log.info('user signed out')
            try {
                await this.signOut()
            } catch (err) {
                Log.info(err)
            }
        })

        Log.logger = console
        Log.level = process.env.NODE_ENV === 'development' ? Log.INFO : Log.ERROR
    }

    async signOutImpl () {
        // setLoggedOut()
        const resp = await this._manager.signoutRedirect()
        Log.info('signed out', resp)
    }

    async renewTokenWithRetries (attempt) {
        try {
            const user = await this._manager.signinSilent()
            if (user === null) {
                return false
            } else {
                return true
            }
        } catch (err) {
            Log.warn('muuuuuuuu: ', err)
            const errorMessage = err.error || err.message || ''
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

    signOut () {
        try {
            return this.signOutImpl()
        } catch (err) {
            Log.info(err)
        }
    }
}

const authService = new AuthService()

export default authService
