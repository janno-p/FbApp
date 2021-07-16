import { AxiosResponse } from 'axios'
import { Notify } from 'quasar'
import { api } from 'src/boot/axios'
// import useAuthentication, { GameResult, IPredictionFixtureType, ITeamType } from 'src/hooks/authentication'
import { computed, defineComponent, ref } from 'vue'
import { useRouter } from 'vue-router'

interface IFixtureType {
    id: number
    homeTeamId: number
    awayTeamId: number
}

type ITeamType = Record<string, unknown>

type GameResult = 'HOME' | 'TIE' | 'AWAY'

interface ITeamWithIdType extends ITeamType {
    id: number
}

interface IFixtureModel {
    id: number
    homeTeam: ITeamWithIdType
    awayTeam: ITeamWithIdType
    result: GameResult | null
    isManual: boolean
}

interface IPredictFixturesResponse {
    competitionId: string
    teams: Record<number, ITeamType>
    fixtures: IFixtureType[]
    groups: Record<string, number[]>
}

class SelectedTeam {
    selected: boolean
    // eslint-disable-next-line no-use-before-define
    qual: QualifierList
    team: ITeamWithIdType
    cb: (t: SelectedTeam) => void
    isManual: boolean

    constructor (team: ITeamWithIdType, qual: QualifierList, cb: (t: SelectedTeam) => void) {
        this.selected = false
        this.team = team
        this.qual = qual
        this.cb = cb
        this.isManual = false
    }

    setSelected (value: boolean, isRandom = false) {
        if (this.selected !== value) {
            this.selected = value
            this.isManual = !isRandom
            this.cb(this)
        }
    }
}

class QualifierList {
    readonly count: number
    selectedCount: number
    teams: Record<string, SelectedTeam[]>

    constructor (teams: Record<string, ITeamWithIdType[]>, count: number) {
        this.count = count
        this.selectedCount = 0
        this.teams = Object.entries(teams).reduce((state: Record<string, SelectedTeam[]>, [k, x]) => {
            state[k] = x.map((t) => new SelectedTeam(t, this, (u) => this.updateCount(u)))
            return state
        }, {})
    }

    get remainingCount () {
        return this.count - this.selectedCount
    }

    get isFull () {
        return this.remainingCount < 1
    }

    updateCount (team: SelectedTeam) {
        this.selectedCount += team.selected ? 1 : -1
    }

    resetRandom () {
        const availableTeams = Object.values(this.teams).reduce((xs, x) => {
            xs.push(...x)
            return xs
        }, []).filter((x) => !x.selected || !x.isManual)
        availableTeams.forEach((x) => x.setSelected(false, true))
    }

    randomizePots () {
        this.resetRandom()
        while (!this.isFull) {
            const teams = Object.values(this.teams).map((x) => {
                const r = (x).filter((u) => !u.selected)
                return r.length > 2 ? r : []
            }).reduce((xs, x) => {
                xs.push(...x)
                return xs
            }, [])
            const i = Math.floor(teams.length * Math.random())
            teams[i].setSelected(true, true)
        }
    }

    randomize (prev: QualifierList | null) {
        if (!prev) {
            this.randomizePots()
            return
        }

        const prevTeams = prev ? Object.values(prev.teams).reduce((xs, x) => {
            xs.push(...x)
            return xs
        }, []).filter((x) => x.selected) : []

        const prevContains = (t: SelectedTeam) => {
            return prevTeams.length === 0 || !!(prevTeams).find((x) => x.team === t.team)
        }

        const teams = Object.values(this.teams).reduce((xs, x) => {
            xs.push(...x)
            return xs
        }, []).filter((x) => prevContains(x) && (!x.selected || !x.isManual))

        teams.forEach((x) => x.setSelected(false, true))

        const r = () => {
            return Math.floor(teams.length * Math.random())
        }

        while (!this.isFull) {
            const i = r()
            teams[i].setSelected(true, true)
            teams.splice(i, 1)
        }
    }
}

export default defineComponent({
    name: 'AddPredictions',

    setup () {
        const stepper = ref<HTMLElement & { next:(() => void) }>()

        // const { googleSignIn, loadPredictions, isSignedIn, setPredictions } = useAuthentication()
        const router = useRouter()

        const teams = ref<ITeamWithIdType[]>([])
        const isSaveInProgress = ref(false)
        const competitionId = ref('')
        const currentStep = ref(0)
        const isLoadingStep = ref(false)
        const fixtures = ref<IFixtureModel[]>([])
        const qualifiers = ref<(QualifierList | null)[]>([])

        const steps = [
            null,
            null,
            {
                title: 'Alagrupist edasipääsejad',
                subtitle: 'Millised meeskonnad jätkavad väljalangemismängudega?',
                buttonText: 'Jätka veerandfinalistide ennustamisega »'
            },
            {
                title: 'Veerandfinalistid',
                subtitle: 'Millised meeskonnad jõuavad veerandfinaalidesse?',
                buttonText: 'Jätka poolfinalistide ennustamisega »'
            },
            {
                title: 'Poolfinalistid',
                subtitle: 'Millised meeskonnad jõuavad poolfinaalidesse?',
                buttonText: 'Jätka finalistide ennustamisega »'
            },
            {
                title: 'Finalistid',
                subtitle: 'Millised on kaks meeskonda, kelle vahel selgitatakse turniiri võitja?',
                buttonText: 'Jätka võitja ennustamisega »'
            },
            {
                title: 'Maailmameister',
                subtitle: 'Milline meeskond on uus maailmameister?',
                buttonText: null
            }
        ]

        const groupStageComplete = computed(() => !!fixtures.value && (fixtures.value || []).every((f) => !!f.result))

        const counterValue = computed(() => {
            const qualifier = qualifiers.value[currentStep.value]
            return displayCounter.value && qualifier !== null ? qualifier.remainingCount : 0
        })

        const displayCounter = computed(() => currentStep.value >= 2 && currentStep.value <= 6)
        const displayRandomizer = computed(() => currentStep.value >= 1 && currentStep.value <= 6)

        async function moveToGroupStage () {
            isLoadingStep.value = true
            const response = await api.get<IPredictFixturesResponse>('/predict/fixtures')
            const responseTeams = Object.values(response.data.teams).map((t, k) => ({ id: k, ...t }))
            teams.value = responseTeams
            competitionId.value = response.data.competitionId
            const responseFixture = response.data.fixtures
                .map<IFixtureModel>((f) => ({
                    id: f.id,
                    homeTeam: { id: f.homeTeamId, ...response.data.teams[f.homeTeamId] },
                    awayTeam: { id: f.awayTeamId, ...response.data.teams[f.awayTeamId] },
                    result: null,
                    isManual: false
                }))
                .filter((f) => !!f.homeTeam && !!f.awayTeam)
            fixtures.value = responseFixture
            const groups = Object.entries(response.data.groups).reduce((state: Record<string, ITeamWithIdType[]>, [k, x]) => {
                state[k] = x.map((t) => ({ id: t, ...response.data.teams[t] }))
                return state
            }, {})
            const responseQualifiers = [
                null,
                null,
                new QualifierList(groups, 16),
                new QualifierList(groups, 8),
                new QualifierList(groups, 4),
                new QualifierList(groups, 2),
                new QualifierList(groups, 1)
            ]
            qualifiers.value = responseQualifiers
            isLoadingStep.value = false

            if (stepper.value) {
                stepper.value.next()
            }
        }

        function setFixtureResult (f: IFixtureModel, result: GameResult) {
            f.result = result
            f.isManual = true
        }

        function randomizeFixtures () {
            const randomize = () : GameResult => {
                const v = Math.floor(Math.random() * 3)
                if (v === 0) {
                    return 'HOME'
                } else if (v === 1) {
                    return 'TIE'
                } else {
                    return 'AWAY'
                }
            }
            fixtures.value.forEach((f) => {
                if (!f.isManual) {
                    f.result = randomize()
                }
            })
        }

        function randomize () {
            if (currentStep.value === 1) {
                randomizeFixtures()
            } else {
                const q = qualifiers.value[currentStep.value]
                if (q) {
                    q.randomize(qualifiers.value[currentStep.value - 1])
                }
            }
        }

        function moveToNextQualRound () {
            if (stepper.value) {
                stepper.value.next()
            }
        }

        function changeSelection (item: SelectedTeam) {
            if (!item.qual.isFull || item.selected) {
                item.setSelected(!item.selected)
            }
        }

        function isDisabled (item: SelectedTeam) {
            return !item.selected && item.qual.isFull
        }

        function getColor (item: SelectedTeam) {
            if (item.selected) {
                return item.isManual ? 'positive' : 'info'
            } else if (item.qual.isFull) {
                return 'negative'
            } else {
                return undefined
            }
        }

        async function registerPrediction () {
            function mapQualifiers (i: number) {
                return Object.values(qualifiers.value[i]?.teams ?? {}).reduce((xs, x) => {
                    xs.push(...x)
                    return xs
                }, []).filter((x) => x.selected).map((x) => x.team.id)
            }

            isSaveInProgress.value = true
            try {
                /*
                if (!isSignedIn) {
                    await googleSignIn()
                }
                */

                const payload = {
                    competitionId: competitionId.value,
                    fixtures: (fixtures.value).map((x) => ({ id: x.id, result: x.result })),
                    qualifiers: {
                        roundOf16: mapQualifiers(2),
                        roundOf8: mapQualifiers(3),
                        roundOf4: mapQualifiers(4),
                        roundOf2: mapQualifiers(5)
                    },
                    winner: mapQualifiers(6)[0]
                }
                await api.post('/predict/', payload)
                /*
                const predictions = {
                    competitionId: payload.competitionId,
                    teams: teams.value,
                    fixtures: (fixtures.value).map<IPredictionFixtureType>((f) => ({
                        fixture: f.id,
                        homeTeam: f.homeTeam.id,
                        awayTeam: f.awayTeam.id,
                        result: f.result
                    })),
                    roundOf16: payload.qualifiers.roundOf16,
                    roundOf8: payload.qualifiers.roundOf8,
                    roundOf4: payload.qualifiers.roundOf4,
                    roundOf2: payload.qualifiers.roundOf2,
                    winner: payload.winner
                }
                setPredictions(predictions)
                */
                await router.push('/')
            } catch (error) {
                const maybeHasResponse = error as { response?: AxiosResponse }
                if (maybeHasResponse.response && maybeHasResponse.response.status === 409) {
                    Notify.create({
                        message: 'Oled juba varasemalt oma ennustuse teinud!',
                        position: 'bottom',
                        type: 'warning',
                        actions: [
                            {
                                label: 'Sulge'
                            }
                        ]
                    })
                    // await loadPredictions()
                    return
                }
                throw error
            } finally {
                isSaveInProgress.value = false
            }
        }

        function toUpperCase (s: string) {
            return s?.toUpperCase() ?? ''
        }

        return {
            changeSelection,
            counterValue,
            displayCounter,
            displayRandomizer,
            getColor,
            groupStageComplete,
            isDisabled,
            moveToGroupStage,
            moveToNextQualRound,
            randomize,
            registerPrediction,
            setFixtureResult,
            stepper,
            steps,
            toUpperCase
        }
    }
})
