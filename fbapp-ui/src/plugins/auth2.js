const gapi = window.gapi

gapi.load('auth2', async () => {
    try {
        await gapi.auth2.init({
            client_id: '83610951178-d4jm3o26r9r40aspvbe9730pjj3nn5d8.apps.googleusercontent.com',
            cookie_policy: 'single_host_origin',
            scope: 'profile email'
        })
    } catch (e) {
        console.error(e)
    }
})

export default () => {
}
