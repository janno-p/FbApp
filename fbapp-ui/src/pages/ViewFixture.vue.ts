import dayjs from 'dayjs'
import { api } from 'src/boot/axios'
import { computed, defineComponent, onUnmounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

interface IFixturePredictionType {
    name: string
    result: string | null
}

interface IQualifierPredictionType {
    name: string
    homeQualifies: boolean
    awayQualifies: boolean
}

interface ITeamType {
    name: string
    flagUrl: string
}

interface IFixtureResponse {
    id: string
    date: string
    previousFixtureId: string | null
    nextFixtureId: string | null
    homeTeam: ITeamType
    awayTeam: ITeamType
    status: string
    stage: string
    fullTime: number[]
    extraTime: number[]
    penalties: number[]
    resultPredictions: IFixturePredictionType[]
    qualifierPredictions: IQualifierPredictionType[]
}

interface IFixtureStatusResponse {
    status: string
    fullTime: number[]
    extraTime: number[]
    penalties: number[]
}

export default defineComponent({
    name: 'ViewFixture',

    setup () {
        const isDestroyed = ref(false)
        const isInitializing = ref(true)
        const fixture = ref<IFixtureResponse>()
        const isLoadingFixture = ref(false)

        const homeGoals = computed(() => {
            const eth = fixture.value?.extraTime ? fixture.value.extraTime[0] : 0
            return fixture.value?.fullTime ? (fixture.value.fullTime[0] + eth) : null
        })

        const awayGoals = computed(() => {
            const eta = fixture.value?.extraTime ? fixture.value.extraTime[1] : 0
            return fixture.value?.fullTime ? (fixture.value.fullTime[1] + eta) : null
        })

        const fixtureStatus = computed(() => {
            if (!fixture.value?.fullTime) {
                return 'None'
            } else if (isHomeWin.value) {
                return 'HomeWin'
            } else if (isAwayWin.value) {
                return 'AwayWin'
            } else {
                return 'Tie'
            }
        })

        const fixtureTitle = computed(() => {
            switch (fixture.value?.status) {
            case 'IN_PLAY':
                return 'Käimasolev mäng'
            case 'FINISHED':
                return 'Lõppenud mäng'
            case 'PAUSED':
                return 'Käimasolev mäng (vaheaeg)'
            default:
                return 'Toimumata mäng'
            }
        })

        const isPreFixture = computed(() => {
            return !fixture.value?.fullTime
        })

        const isHomeWin = computed(() => {
            if (!fixture.value?.fullTime) {
                return false
            }
            const hg = fixture.value?.fullTime[0] + (fixture.value.penalties ? fixture.value.penalties[0] : 0)
            const ag = fixture.value?.fullTime[1] + (fixture.value.penalties ? fixture.value.penalties[1] : 0)
            return hg > ag
        })

        const isAwayWin = computed(() => {
            if (!fixture.value?.fullTime) {
                return false
            }
            const hg = fixture.value?.fullTime[0] + (fixture.value.penalties ? fixture.value.penalties[0] : 0)
            const ag = fixture.value?.fullTime[1] + (fixture.value.penalties ? fixture.value.penalties[1] : 0)
            return hg < ag
        })

        const isDraw = computed(() => {
            if (!fixture.value?.fullTime) {
                return false
            }
            const hg = fixture.value?.fullTime[0] + (fixture.value.penalties ? fixture.value.penalties[0] : 0)
            const ag = fixture.value?.fullTime[1] + (fixture.value.penalties ? fixture.value.penalties[1] : 0)
            return hg === ag
        })

        function formatStage (stage: string) {
            switch (stage) {
            case 'GROUP_STAGE':
                return 'Alagrupimäng'
            case 'LAST_16':
                return '1/16 finaal'
            case 'QUARTER_FINAL':
                return 'Veerandfinaal'
            case 'SEMI_FINAL':
                return 'Poolfinaal'
            case 'FINAL':
                return 'Finaal'
            default:
                return ''
            }
        }

        function goals (value: number | null) {
            return value === null ? '-' : value
        }

        async function updateFixture () {
            if (fixture.value) {
                const response = await api.get<IFixtureStatusResponse>(`/fixtures/${fixture.value.id}/status`)
                if (response.data) {
                    fixture.value.status = response.data.status
                    fixture.value.fullTime = response.data.fullTime
                    fixture.value.extraTime = response.data.extraTime
                    fixture.value.penalties = response.data.penalties
                }
            }
        }

        function runUpdate () {
            setTimeout(() => {
                if (!isDestroyed.value) {
                    void updateFixture()
                        .finally(() => runUpdate())
                }
            }, 30000)
        }

        function formatDate (d: string) {
            return dayjs(d).format('DD.MM.YYYY HH:mm')
        }

        function isCorrectResultPrediction (prediction: IFixturePredictionType) {
            return fixtureStatus.value === prediction.result
        }

        function predictionText (prediction: IFixturePredictionType) {
            switch (prediction.result) {
            case 'HomeWin':
                return fixture.value?.homeTeam.name
            case 'AwayWin':
                return fixture.value?.awayTeam.name
            case 'Tie':
                return 'Draw'
            }
        }

        async function loadFixture (id: string) {
            try {
                isLoadingFixture.value = true
                const response = await api.get<IFixtureResponse>(`/fixtures/${id}`)
                fixture.value = response.data
            } finally {
                isLoadingFixture.value = false
            }
        }

        const route = useRoute()
        const router = useRouter()

        const fixtureId = computed(() => {
            const fixtureId: string | undefined = route.params.fixtureId as string | undefined
            return fixtureId
        })

        watch(fixtureId, async (id) => {
            try {
                if (id) {
                    await loadFixture(id)
                } else {
                    const response = await api.get<IFixtureResponse>('/fixtures/timely')
                    fixture.value = response.data
                }
            } finally {
                runUpdate()
                isInitializing.value = false
            }
        }, { immediate: true })

        async function openPrevious () {
            if (fixture.value?.previousFixtureId) {
                await router.push({ name: 'fixture', params: { fixtureId: fixture.value.previousFixtureId } })
            }
        }

        async function openNext () {
            if (fixture.value?.nextFixtureId) {
                await router.push({ name: 'fixture', params: { fixtureId: fixture.value.nextFixtureId } })
            }
        }

        function handleKeyboardInput (event: KeyboardEvent) {
            switch (event.which) {
            case 37:
                void openPrevious()
                break
            case 39:
                void openNext()
                break
            }
        }

        function awayQualifiesIcon (prediction: IQualifierPredictionType) {
            return prediction.awayQualifies ? 'done' : 'close'
        }

        function homeQualifiesIcon (prediction: IQualifierPredictionType) {
            return prediction.homeQualifies ? 'done' : 'close'
        }

        function awayQualifiesResultClass (prediction: IQualifierPredictionType) {
            if (isPreFixture.value) {
                return undefined
            } else if (isDraw.value) {
                return 'warning'
            } else if (isHomeWin.value) {
                return !prediction.awayQualifies ? 'positive' : 'negative'
            } else {
                return prediction.awayQualifies ? 'positive' : 'negative'
            }
        }

        function homeQualifiesResultClass (prediction: IQualifierPredictionType) {
            if (isPreFixture.value) {
                return undefined
            } else if (isDraw.value) {
                return 'warning'
            } else if (isAwayWin.value) {
                return !prediction.homeQualifies ? 'positive' : 'negative'
            } else {
                return prediction.homeQualifies ? 'positive' : 'negative'
            }
        }

        window.addEventListener('keyup', handleKeyboardInput)

        onUnmounted(() => {
            window.removeEventListener('keyup', handleKeyboardInput)
            isDestroyed.value = true
        })

        return {
            awayGoals,
            awayQualifiesIcon,
            awayQualifiesResultClass,
            fixture,
            fixtureTitle,
            formatDate,
            formatStage,
            goals,
            homeGoals,
            homeQualifiesIcon,
            homeQualifiesResultClass,
            isCorrectResultPrediction,
            isInitializing,
            isLoadingFixture,
            isPreFixture,
            predictionText
        }
    }
})
