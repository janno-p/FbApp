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
        return Promise.reject(error)
    }
)

export default ({ Vue }) => {
    Vue.prototype.$axios = $axios
}
