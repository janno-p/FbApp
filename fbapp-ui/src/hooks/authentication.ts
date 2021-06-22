import { api } from 'boot/axios'
import { computed, ref } from 'vue'
import { AxiosResponse } from 'axios'
import { Notify } from 'quasar'

export type GameResult = 'HOME' | 'AWAY' | 'TIE'

interface IUserType {
    email: string
    picture: string
    name: string
    roles: string[]
    xsrfToken: string
}

export interface ITeamType {
    name: string
    flagUrl: string
}

export interface IPredictionFixtureType {
    fixture: number
    homeTeam: number
    awayTeam: number
    result: GameResult | null
}

interface IPredictionType {
    competitionId: string
    teams: Record<number, ITeamType>
    fixtures: IPredictionFixtureType[]
    roundOf16: number[]
    roundOf8: number[]
    roundOf4: number[]
    roundOf2: number[]
    winner: number
}

interface IBootstrapResponse {
    user: IUserType
    competitionStatus: string
}

const isSignedIn = ref(false)
const email = ref('')
const name = ref('')
const imageUrl = ref('')
const roles = ref<string[]>([])
const isLoadingPredictions = ref(false)
const predictions = ref<IPredictionType>()
const competitionStatus = ref('')
const isGoogleReady = ref(false)

function cleanPictureUrl (url?: string) {
    if (url) {
        const indexOf = url.lastIndexOf('=')
        if (indexOf >= 0) {
            return url.substring(0, indexOf)
        } else {
            return url
        }
    } else {
        return ''
    }
}

function setUser (payload?: IUserType) {
    isSignedIn.value = !!payload
    email.value = payload ? payload.email : ''
    imageUrl.value = cleanPictureUrl(payload?.picture)
    name.value = payload ? payload.name : ''
    roles.value = payload ? payload.roles : []
    if (payload) {
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
        api.defaults.headers = { ...api.defaults.headers, 'X-XSRF-TOKEN': payload.xsrfToken }
    } else {
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment
        api.defaults.headers = { ...api.defaults.headers, 'X-XSRF-TOKEN': undefined }
    }
}

function setLoadingPredictions (isLoading: boolean) {
    isLoadingPredictions.value = isLoading
}

function setPredictions (payload?: IPredictionType) {
    predictions.value = payload
}

function setCompetitionStatus (value: string) {
    competitionStatus.value = value
}

function setGoogleReady (value: boolean) {
    isGoogleReady.value = value
}

const hasDashboard = computed(() => roles.value.includes('Administrator'))

async function googleSignIn () {
    try {
        const auth = window.gapi.auth2.getAuthInstance()
        const googleUser = await auth.signIn()
        const response = await api.post<IUserType>('/auth/signin', {
            idToken: googleUser.getAuthResponse().id_token
        })
        setUser(response.data)
    } catch (error) {
        let message = JSON.stringify(error)
        const maybeHasResponse = error as { response?: AxiosResponse<unknown> }
        if (maybeHasResponse.response) {
            const response = maybeHasResponse.response
            message = `(${response.statusText}) ${JSON.stringify(response.data)}`
        }
        Notify.create({
            message: `Google kontoga sisselogimine ebaõnnestus: ${message}`,
            position: 'bottom',
            type: 'negative',
            actions: [
                {
                    label: 'Sulge'
                }
            ]
        })
    }
    await loadPredictions()
}

async function googleSignOut () {
    try {
        const auth = window.gapi.auth2.getAuthInstance()
        auth.disconnect()
        await api.post('/auth/signout', {})
        setUser()
        setPredictions()
    } catch (e) {
        console.error(e)
    }
}

async function loadPredictions () {
    try {
        setLoadingPredictions(true)
        const response = await api.get<IPredictionType>('/predict/current')
        setPredictions(response.data)
    } finally {
        setLoadingPredictions(false)
    }
}

async function authenticate () {
    try {
        const response = await api.get<IBootstrapResponse>('/bootstrap')
        setUser(response.data.user)
        const competitionStatus = response.data.competitionStatus
        setCompetitionStatus(competitionStatus)
        setGoogleReady(true)
    } catch (e) {
        console.error(e)
    }
    if (isSignedIn.value) {
        await loadPredictions()
    }
}

export default function useAuthentication () {
    return {
        authenticate,
        competitionStatus,
        googleSignIn,
        googleSignOut,
        hasDashboard,
        imageUrl,
        isGoogleReady,
        isLoadingPredictions,
        isSignedIn,
        loadPredictions,
        name,
        predictions,
        setPredictions
    }
}
