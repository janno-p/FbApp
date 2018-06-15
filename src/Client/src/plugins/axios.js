import axios from "axios"
import { Notify } from "quasar"

const $axios = axios.create({
    baseURL: process.env.API
})

$axios.interceptors.response.use(
    undefined,
    (error) => {
        if (error.response.status === 403) {
            Notify.create({
                message: "Juurdepääs keelatud!",
                position: "bottom",
                type: "negative",
                actions: [
                    {
                        label: "Sulge"
                    }
                ]
            })
        }
        if (error.response.status === 409) {
            Notify.create({
                message: "Andmete vastuolu: juba olemas.",
                position: "bottom",
                type: "warning",
                actions: [
                    {
                        label: "Sulge"
                    }
                ]
            })
        }
        return Promise.reject(error)
    }
)

$axios.interceptors.request.use(
    (config) => {
        const xsrfToken = localStorage.getItem("XSRF-TOKEN")
        if (xsrfToken) {
            config.headers["X-XSRF-TOKEN"] = xsrfToken
        }
        return config
    }
)

export default ({ Vue }) => {
    Vue.prototype.$axios = $axios
}
