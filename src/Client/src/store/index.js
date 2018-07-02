import { Notify } from "quasar"
import Vue from "vue"
import Vuex from "vuex"

import example from "./module-example"
import { SET_COMPETITION_STATUS, SET_GOOGLE_READY, SET_USER } from "./mutation-types"

Vue.use(Vuex)

const store = new Vuex.Store({
    actions: {
        async authenticate (context) {
            try {
                const response = await this._vm.$axios.get("/bootstrap")
                context.commit(SET_USER, response.data.user)
                const competitionStatus = response.data.competitionStatus
                context.commit(SET_COMPETITION_STATUS, { competitionStatus })
                context.commit(SET_GOOGLE_READY, { isReady: true })
            } catch (e) {
                console.error(e)
            }
        },

        async googleSignIn (context) {
            try {
                const auth = window.gapi.auth2.getAuthInstance()
                const googleUser = await auth.signIn()
                const response = await this._vm.$axios.post("/auth/signin", {
                    idToken: googleUser.getAuthResponse().id_token
                })
                context.commit(SET_USER, response.data)
            } catch (error) {
                let message = JSON.stringify(error)
                if (error.response) {
                    message = `(${error.response.statusText}) ${JSON.stringify(error.response.data)}`
                }
                Notify.create({
                    message: `Google kontoga sisselogimine ebaõnnestus: ${message}`,
                    position: "bottom",
                    type: "negative",
                    actions: [
                        {
                            label: "Sulge"
                        }
                    ]
                })
            }
        },

        async googleSignOut (context) {
            try {
                const auth = window.gapi.auth2.getAuthInstance()
                auth.disconnect()
                await this._vm.$axios.post("/auth/signout", {})
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
        [SET_COMPETITION_STATUS] (state, { competitionStatus }) {
            state.competitionStatus = competitionStatus
        },

        [SET_GOOGLE_READY] (state, { isReady }) {
            state.isGoogleReady = isReady
        },

        [SET_USER] (state, payload) {
            state.isSignedIn = !!payload
            state.email = payload ? payload.email : ""
            state.imageUrl = payload ? payload.picture : ""
            state.name = payload ? payload.name : ""
            state.roles = payload ? payload.roles : []
            if (payload) {
                localStorage.setItem("XSRF-TOKEN", payload.xsrfToken)
            } else {
                localStorage.removeItem("XSRF-TOKEN")
            }
        }
    },

    state: {
        competitionStatus: "",
        isGoogleReady: false,
        isSignedIn: false,
        name: "",
        imageUrl: "",
        email: "",
        roles: []
    }
})

export default store
