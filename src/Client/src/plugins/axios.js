import axios from "axios"

const $axios = axios.create({
    baseURL: process.env.API
})

export default ({ Vue }) => {
    Vue.prototype.$axios = $axios
}
