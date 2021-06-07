import { UserManager } from 'oidc-client'

const userManager = new UserManager({
    loadUserInfo: true,
    filterProtocolClaims: true
})

void userManager.signinSilentCallback()
