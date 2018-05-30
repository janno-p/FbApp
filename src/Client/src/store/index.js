import Vue from "vue"
import Vuex from "vuex"

import example from "./module-example"
import { SET_GOOGLE_READY, SET_USER } from "./mutation-types"

Vue.use(Vuex)

const store = new Vuex.Store({
    actions: {
        async authenticate (context) {
            try {
                const response = await this._vm.$axios.post("/tokeninfo", {})
                context.commit(SET_USER, response.data)
                context.commit(SET_GOOGLE_READY, { isReady: true })
            } catch (e) {
                console.error(e)
            }
        },

        async googleSignIn (context) {
            try {
                const auth = window.gapi.auth2.getAuthInstance()
                const googleUser = await auth.signIn()
                const response = await this._vm.$axios.post("/tokensignin", {
                    idToken: googleUser.getAuthResponse().id_token
                })
                context.commit(SET_USER, response.data)
            } catch (e) {
                console.error(e)
            }
        },

        async googleSignOut (context) {
            try {
                const auth = window.gapi.auth2.getAuthInstance()
                auth.disconnect()
                await this._vm.$axios.post("/tokensignout", {})
                context.commit(SET_USER, null)
            } catch (e) {
                console.error(e)
            }
        }
    },

    getters: {
        hasDashboard (state) {
            return state.roles.includes("Administrator")
        }
    },

    modules: {
        example
    },

    mutations: {
        [SET_GOOGLE_READY] (state, { isReady }) {
            state.isGoogleReady = isReady
        },

        [SET_USER] (state, payload) {
            state.isSignedIn = !!payload
            state.email = payload ? payload.email : ""
            state.imageUrl = payload ? payload.picture : ""
            state.name = payload ? payload.name : ""
            state.roles = payload ? payload.roles : []
        }
    },

    state: {
        isGoogleReady: false,
        isSignedIn: false,
        name: "",
        imageUrl: "",
        email: "",
        roles: []
    }
})

export default store
