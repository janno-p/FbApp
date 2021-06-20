import dayjs from 'dayjs'
import { api } from 'src/boot/axios'
import { GameResult } from 'src/hooks/authentication'
import { computed, defineComponent, nextTick, onMounted, onUnmounted, ref } from 'vue'

interface IFixturePredictionType {
    name: string
    result: GameResult | null
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
                return 'NONE'
            } else if (isHomeWin.value) {
                return 'HOME'
            } else if (isAwayWin.value) {
                return 'AWAY'
            } else {
                return 'TIE'
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
                    try {
                        void updateFixture()
                    } finally {
                        runUpdate()
                    }
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
            case 'HOME':
                return fixture.value?.homeTeam.name
            case 'AWAY':
                return fixture.value?.awayTeam.name
            case 'TIE':
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

        async function openPrevious () {
            if (fixture.value?.previousFixtureId) {
                await loadFixture(fixture.value.previousFixtureId)
            }
        }

        async function openNext () {
            if (fixture.value?.nextFixtureId) {
                await loadFixture(fixture.value.nextFixtureId)
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

        onMounted(() => {
            void nextTick(() => {
                try {
                    void api.get<IFixtureResponse>('/fixtures/timely').then((response) => {
                        fixture.value = response.data
                    })
                } finally {
                    runUpdate()
                    isInitializing.value = false
                }
            })
        })

        onUnmounted(() => {
            window.removeEventListener('keyup', handleKeyboardInput)
            isDestroyed.value = true
        })

        return {
            awayGoals,
            awayQualifiesIcon,
            awayQualifiesResultClass,
            fixtureTitle,
            formatDate,
            formatStage,
            goals,
            homeGoals,
            homeQualifiesIcon,
            homeQualifiesResultClass,
            isCorrectResultPrediction,
            predictionText
        }
    }
})
