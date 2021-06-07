import { UserManager } from 'oidc-client'

const mgr = new UserManager({
    loadUserInfo: true,
    filterProtocolClaims: true
})

mgr.signinSilentCallback()
