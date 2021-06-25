import { boot } from 'quasar/wrappers'
import useAuthentication from 'src/hooks/authentication'

export default boot(() => {
    return new Promise((resolve, reject) => {
        const gapi = window.gapi

        function onInit () {
            const { authenticate } = useAuthentication()
            void authenticate().then(resolve)
        }

        function onError (e: unknown) {
            console.error(e)
            reject()
        }

        gapi.load('auth2', () => {
            void gapi.auth2.init({
                client_id: '83610951178-d4jm3o26r9r40aspvbe9730pjj3nn5d8.apps.googleusercontent.com',
                cookie_policy: 'single_host_origin',
                scope: 'profile email'
            }).then(onInit, onError)
        })
    })
})
