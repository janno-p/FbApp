module Page.Fixture exposing (Model, Msg, init, update, view)

import Api.Endpoint as Endpoint exposing (defaultEndpointConfig, fixture)
import DateFormat
import Html exposing (Html, button, div, span, table, tbody, td, text, tr)
import Html.Attributes exposing (class, disabled, title)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (required)
import Page exposing (PageTab(..), viewResultsTabs)
import Route
import Session exposing (Session)
import Task
import Team exposing (estonianName, flagClass)
import Time exposing (Posix, Zone, utc)
import Url exposing (Protocol(..))


type alias Model =
    { fixtureId : Maybe String
    , fixture : Maybe Fixture
    , timeZone : Zone
    }


type FixtureStage
    = GroupStage
    | RoundOf32
    | RoundOf16
    | QuarterFinals
    | SemiFinals
    | Final
    | Unknown


type FixtureStatus
    = InPlay
    | Finished
    | Paused
    | Pending


type FixtureResult
    = HomeWin
    | Tie
    | AwayWin


type QualifierSide
    = Home Bool
    | Away Bool


type alias Team =
    { name : String
    , tla : String
    }


type alias FixtureResultPrediction =
    { name : String
    , result : FixtureResult
    , isBoosted : Bool
    }


type alias QualifierPrediction =
    { name : String
    , homeQualifies : Bool
    , awayQualifies : Bool
    }


type alias Fixture =
    { previousFixtureId : Maybe String
    , nextFixtureId : Maybe String
    , status : FixtureStatus
    , homeTeam : Team
    , awayTeam : Team
    , stage : FixtureStage
    , date : Posix
    , extraTime : Maybe ( Int, Int )
    , fullTime : Maybe ( Int, Int )
    , penalties : Maybe ( Int, Int )
    , resultPredictions : List FixtureResultPrediction
    , qualifierPredictions : List QualifierPrediction
    }


init : Session -> Maybe String -> ( Model, Cmd Msg )
init session fixtureId =
    let
        model =
            { fixtureId = fixtureId
            , fixture = Nothing
            , timeZone = utc
            }
    in
    ( model
    , Cmd.batch
        [ loadFixture session model
        , Task.attempt SetZone Time.here
        ]
    )


view : Model -> { title : String, content : Html Msg }
view model =
    let
        content =
            div []
                [ viewResultsTabs FixtureTab
                , div [ class "sm:rounded-md sm:border border-gray-200 sm:w-160 sm:mx-auto mt-2 sm:mt-8 sm:shadow-lg overflow-hidden" ]
                    [ case model.fixture of
                        Just fixture ->
                            viewFixture model.timeZone fixture

                        Nothing ->
                            div [ class "mt-8 mb-8 text-center" ] [ text "Laadimine ..." ]
                    ]
                ]
    in
    { title = "Mängude tulemused"
    , content = content
    }


viewFixture : Zone -> Fixture -> Html Msg
viewFixture zone fixture =
    let
        ( penHome, penAway ) =
            fixture.penalties |> Maybe.withDefault ( 0, 0 )

        expectedResult =
            fixture.fullTime
                |> Maybe.map
                    (\( ho, aw ) ->
                        if ho + penHome > aw + penAway then
                            HomeWin

                        else if ho + penHome < aw + penAway then
                            AwayWin

                        else
                            Tie
                    )

        resultPredictions =
            List.map (viewResultPrediction fixture expectedResult) fixture.resultPredictions

        qualifierPredictions =
            List.map (viewPlayOffPrediction expectedResult) fixture.qualifierPredictions
    in
    div []
        [ viewFixtureHero zone fixture
        , table [ class "w-full mt-4 mb-8" ]
            [ tbody [] (resultPredictions ++ qualifierPredictions) ]
        ]


viewFixtureHero : Zone -> Fixture -> Html Msg
viewFixtureHero zone fixture =
    div [ class ("relative overflow-hidden px-4 py-5 sm:px-6 sm:py-6 text-white shadow-xl " ++ heroSurfaceClass fixture.status) ]
        [ div [ class "relative z-10 grid grid-cols-[1fr_auto_1fr] items-center gap-2 text-[0.65rem] sm:text-xs uppercase tracking-[0.16em] text-blue-100" ]
            [ viewHeroNavButton "Eelmine mäng" "icon-[mdi--arrow-left]" "justify-self-start text-left" fixture.previousFixtureId
            , div [ class ("rounded-full border px-3 py-2 font-extrabold text-white " ++ statusChipClass fixture.status) ] [ text (fixtureStatusLabel fixture.status) ]
            , viewHeroNavButton "Järgmine mäng" "icon-[mdi--arrow-right]" "justify-self-end text-right" fixture.nextFixtureId
            ]
        , div [ class "relative z-10 mt-5 text-center text-[0.65rem] sm:text-xs uppercase tracking-[0.18em] text-blue-100/90" ]
            [ text (fixtureStage fixture ++ " · " ++ dateFormatter zone fixture.date) ]
        , div [ class "relative z-10 mt-5 grid grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] items-center gap-3 sm:gap-5" ]
            [ viewHeroTeam fixture.homeTeam
            , viewHeroScorePanel zone fixture
            , viewHeroTeam fixture.awayTeam
            ]
        ]


viewHeroNavButton : String -> String -> String -> Maybe String -> Html Msg
viewHeroNavButton label iconClass alignment maybeFixtureId =
    case maybeFixtureId of
        Just fixtureId ->
            button
                [ title label
                , onClick (LoadFixture fixtureId)
                , class (alignment ++ " inline-flex items-center gap-1 rounded-full px-2 py-2 text-blue-100 hover:bg-white/10 hover:text-white cursor-pointer")
                ]
                [ span [ class iconClass ] []
                , span [ class "hidden sm:inline" ] [ text label ]
                ]

        Nothing ->
            button
                [ title label
                , disabled True
                , class (alignment ++ " inline-flex cursor-default items-center gap-1 rounded-full px-2 py-2 text-white/25")
                ]
                [ span [ class iconClass ] []
                , span [ class "hidden sm:inline" ] [ text label ]
                ]


fixtureStatusLabel : FixtureStatus -> String
fixtureStatusLabel status =
    case status of
        InPlay ->
            "Käimasolev"

        Paused ->
            "Vaheaeg"

        Finished ->
            "Lõppenud"

        Pending ->
            "Tulekul"


heroSurfaceClass : FixtureStatus -> String
heroSurfaceClass status =
    case status of
        InPlay ->
            "bg-[radial-gradient(circle_at_top_left,rgba(33,186,69,0.34),transparent_38%),radial-gradient(circle_at_bottom_right,rgba(242,192,55,0.24),transparent_42%),linear-gradient(135deg,#052e16_0%,#0f172a_100%)]"

        Paused ->
            "bg-[radial-gradient(circle_at_top_left,rgba(33,186,69,0.28),transparent_38%),linear-gradient(135deg,#064e3b_0%,#0f172a_100%)]"

        Finished ->
            "bg-[radial-gradient(circle_at_top_left,rgba(38,166,154,0.42),transparent_38%),linear-gradient(135deg,#0f172a_0%,#1e3a8a_100%)]"

        Pending ->
            "bg-[radial-gradient(circle_at_top_left,rgba(49,204,236,0.34),transparent_38%),linear-gradient(135deg,#111827_0%,#1e3a8a_100%)]"


statusChipClass : FixtureStatus -> String
statusChipClass status =
    case status of
        InPlay ->
            "border-green-200/40 bg-green-500/20"

        Paused ->
            "border-green-200/30 bg-green-500/15"

        Finished ->
            "border-white/20 bg-white/15"

        Pending ->
            "border-sky-200/30 bg-sky-500/15"


viewHeroTeam : Team -> Html Msg
viewHeroTeam team =
    div [ class "min-w-0 text-center" ]
        [ span (flagClass team.tla ++ [ class "mx-auto h-7 sm:h-9 drop-shadow text-4xl", title (estonianName team) ]) []
        , div [ class "mt-2 truncate text-xs font-extrabold sm:text-base" ] [ text (estonianName team) ]
        ]


viewHeroScorePanel : Zone -> Fixture -> Html Msg
viewHeroScorePanel zone fixture =
    let
        detail =
            fixtureScoreDetail fixture
    in
    div [ class "min-w-24 rounded-2xl border border-white/20 bg-white/15 px-3 py-4 text-center shadow-lg backdrop-blur sm:min-w-32 sm:px-4" ]
        (case fixture.status of
            Pending ->
                [ div [ class "text-2xl font-black tracking-tight sm:text-4xl" ] [ text (timeFormatter zone fixture.date) ]
                , div [ class "mt-2 text-[0.65rem] uppercase tracking-[0.16em] text-blue-100" ] [ text "Algusaeg" ]
                ]

            _ ->
                case fixture.fullTime of
                    Just score ->
                        div [ class "text-3xl font-black tracking-[-0.08em] sm:text-5xl" ] [ text (scorePairText score) ]
                            :: detail

                    Nothing ->
                        [ div [ class "text-xs font-black uppercase tracking-[0.12em] sm:text-sm" ] [ text "Tulemus puudub" ] ]
        )


fixtureScoreDetail : Fixture -> List (Html Msg)
fixtureScoreDetail fixture =
    let
        extraTimeDetail =
            case meaningfulExtraTimeScore fixture of
                Just extraTime ->
                    [ div [ class "mt-2 text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-blue-100" ]
                        [ text ("Lisaajal " ++ scorePairText extraTime) ]
                    ]

                Nothing ->
                    []

        penaltyDetail =
            case fixture.penalties of
                Just ( home, away ) ->
                    [ div [ class "mt-2 text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-blue-100" ]
                        [ text ("Pen " ++ String.fromInt home ++ " : " ++ String.fromInt away) ]
                    ]

                Nothing ->
                    []
    in
    extraTimeDetail ++ penaltyDetail


meaningfulExtraTimeScore : Fixture -> Maybe ( Int, Int )
meaningfulExtraTimeScore fixture =
    case ( fixture.extraTime, fixture.fullTime ) of
        ( Just extraTime, Just fullTime ) ->
            if extraTime /= fullTime then
                Just extraTime

            else
                Nothing

        _ ->
            Nothing


scorePairText : ( Int, Int ) -> String
scorePairText ( home, away ) =
    String.fromInt home ++ " : " ++ String.fromInt away


timeFormatter : Zone -> Posix -> String
timeFormatter =
    DateFormat.format
        [ DateFormat.hourMilitaryFixed
        , DateFormat.text ":"
        , DateFormat.minuteFixed
        ]


viewResultPrediction : Fixture -> Maybe FixtureResult -> FixtureResultPrediction -> Html Msg
viewResultPrediction fixture expectedResult fixtureResult =
    let
        predictionText =
            case fixtureResult.result of
                HomeWin ->
                    text (estonianName fixture.homeTeam)

                AwayWin ->
                    text (estonianName fixture.awayTeam)

                Tie ->
                    text "Viik"
    in
    tr [ boosterRowClass expectedResult fixtureResult ]
        [ td [ class "px-4 w-14" ] [ viewResultIcon expectedResult fixtureResult.result ]
        , td [ class "capitalize" ] [ text fixtureResult.name ]
        , td [] [ predictionText ]
        , td [ class "py-2 pr-3 text-right" ] (viewBoosterIcon expectedResult fixtureResult)
        ]


boosterRowClass : Maybe FixtureResult -> FixtureResultPrediction -> Html.Attribute Msg
boosterRowClass expectedResult predictedResult =
    let
        baseClass =
            "border-b align-middle"
    in
    if not predictedResult.isBoosted then
        class baseClass

    else
        case expectedResult of
            Nothing ->
                class (baseClass ++ " bg-violet-50 shadow-[inset_4px_0_0_#7c3aed]")

            Just result ->
                if result == predictedResult.result then
                    class (baseClass ++ " bg-green-50 shadow-[inset_4px_0_0_#22c55e]")

                else
                    class (baseClass ++ " bg-red-50 shadow-[inset_4px_0_0_#ef4444]")


viewResultIcon : Maybe FixtureResult -> FixtureResult -> Html Msg
viewResultIcon expectedResult predictedResult =
    case expectedResult of
        Just result ->
            if result == predictedResult then
                span [ class "icon-[mdi--check] text-green-500 text-2xl" ] []

            else
                span [ class "icon-[mdi--close] text-red-500 text-2xl" ] []

        Nothing ->
            span [ class "icon-[mdi--minus] text-gray-300 text-2xl" ] []


viewBoosterIcon : Maybe FixtureResult -> FixtureResultPrediction -> List (Html Msg)
viewBoosterIcon expectedResult predictedResult =
    if not predictedResult.isBoosted then
        []

    else
        case expectedResult of
            Nothing ->
                [ viewBoosterPill "icon-[mdi--syringe]" "Topeltpanus mängus" "bg-violet-100 text-violet-800" ]

            Just result ->
                if result == predictedResult.result then
                    [ viewBoosterPill "icon-[mdi--syringe]" "Topeltpanus tabas" "bg-green-100 text-green-800" ]

                else
                    [ viewBoosterPill "icon-[mdi--syringe-off]" "Topeltpanus möödas" "bg-red-100 text-red-800" ]


viewBoosterPill : String -> String -> String -> Html Msg
viewBoosterPill iconClass label colorClass =
    span
        [ class ("inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-[0.7rem] sm:text-xs font-extrabold whitespace-nowrap " ++ colorClass) ]
        [ span [ class (iconClass ++ " text-sm") ] []
        , span [] [ text label ]
        ]


qualifierIconClass : Bool -> Html.Attribute Msg
qualifierIconClass expectQualifies =
    if expectQualifies then
        class "icon-[mdi--check]"

    else
        class "icon-[mdi--close]"


qualifierColorClass : Maybe FixtureResult -> QualifierSide -> Html.Attribute Msg
qualifierColorClass maybeFixtureResult qualifierSide =
    case ( maybeFixtureResult, qualifierSide ) of
        ( Nothing, _ ) ->
            class "text-gray-200"

        ( Just Tie, _ ) ->
            class "text-orange-400"

        ( Just HomeWin, Home True ) ->
            class "text-green-500"

        ( Just HomeWin, Away False ) ->
            class "text-green-500"

        ( Just AwayWin, Away True ) ->
            class "text-green-500"

        ( Just AwayWin, Home False ) ->
            class "text-green-500"

        ( Just _, _ ) ->
            class "text-red-500"


viewPlayOffPrediction : Maybe FixtureResult -> QualifierPrediction -> Html Msg
viewPlayOffPrediction expectedResult prediction =
    tr [ class "border-b" ]
        [ td [ class "px-4 w-32 text-center" ]
            [ span [ qualifierIconClass prediction.homeQualifies, class "text-2xl", qualifierColorClass expectedResult (Home prediction.homeQualifies) ] []
            ]
        , td [ class "capitalize text-center" ] [ text prediction.name ]
        , td [ class "px-4 w-32 text-center" ]
            [ span [ qualifierIconClass prediction.awayQualifies, class "text-2xl", qualifierColorClass expectedResult (Away prediction.awayQualifies) ] []
            ]
        ]


type Msg
    = FixtureLoaded (Result Http.Error Fixture)
    | SetZone (Result () Zone)
    | LoadFixture String


update : Session -> Msg -> Model -> ( Model, Cmd Msg )
update session msg model =
    case msg of
        FixtureLoaded (Ok fixture) ->
            ( { model | fixture = Just fixture }, Cmd.none )

        FixtureLoaded _ ->
            ( model, Cmd.none )

        SetZone result ->
            ( { model | timeZone = Result.withDefault utc result }, Cmd.none )

        LoadFixture fixtureId ->
            let
                updated =
                    { model | fixtureId = Just fixtureId, fixture = Nothing }
            in
            ( updated, Route.replaceUrl (Session.navKey session) (Route.Fixture (Just fixtureId)) )


loadFixture : Session -> Model -> Cmd Msg
loadFixture session model =
    let
        endpoint =
            model.fixtureId
                |> Maybe.map Endpoint.fixture
                |> Maybe.withDefault Endpoint.defaultFixture
    in
    Endpoint.request
        endpoint
        (Http.expectJson FixtureLoaded fixtureDecoder)
        { defaultEndpointConfig | headers = Endpoint.useToken session }


scoreDecoder : Json.Decoder (Maybe ( Int, Int ))
scoreDecoder =
    Json.list Json.int
        |> Json.map
            (\arr ->
                case arr of
                    home :: away :: _ ->
                        ( home, away )

                    _ ->
                        ( 0, 0 )
            )
        |> Json.nullable


fixtureDecoder : Json.Decoder Fixture
fixtureDecoder =
    Json.succeed Fixture
        |> required "previousFixtureId" (Json.nullable Json.string)
        |> required "nextFixtureId" (Json.nullable Json.string)
        |> required "status" fixtureStatusDecoder
        |> required "homeTeam" teamDecoder
        |> required "awayTeam" teamDecoder
        |> required "stage" fixtureStageDecoder
        |> required "date" (Json.int |> Json.map Time.millisToPosix)
        |> required "extraTime" scoreDecoder
        |> required "fullTime" scoreDecoder
        |> required "penalties" scoreDecoder
        |> required "resultPredictions" (Json.list fixtureResultPredictionDecoder)
        |> required "qualifierPredictions" (Json.list qualifierPredictionDecoder)


qualifierPredictionDecoder : Json.Decoder QualifierPrediction
qualifierPredictionDecoder =
    Json.succeed QualifierPrediction
        |> required "name" Json.string
        |> required "homeQualifies" Json.bool
        |> required "awayQualifies" Json.bool


fixtureResultPredictionDecoder : Json.Decoder FixtureResultPrediction
fixtureResultPredictionDecoder =
    Json.succeed FixtureResultPrediction
        |> required "name" Json.string
        |> required "result" fixtureResultDecoder
        |> required "isBoosted" Json.bool


fixtureResultDecoder : Json.Decoder FixtureResult
fixtureResultDecoder =
    Json.string
        |> Json.map
            (\value ->
                case value of
                    "HomeWin" ->
                        HomeWin

                    "AwayWin" ->
                        AwayWin

                    _ ->
                        Tie
            )


fixtureStageDecoder : Json.Decoder FixtureStage
fixtureStageDecoder =
    Json.string
        |> Json.map
            (\value ->
                case value of
                    "LAST_32" ->
                        RoundOf32

                    "LAST_16" ->
                        RoundOf16

                    "QUARTER_FINALS" ->
                        QuarterFinals

                    "SEMI_FINALS" ->
                        SemiFinals

                    "FINAL" ->
                        Final

                    _ ->
                        GroupStage
            )


fixtureStatusDecoder : Json.Decoder FixtureStatus
fixtureStatusDecoder =
    Json.string
        |> Json.map
            (\value ->
                case value of
                    "IN_PLAY" ->
                        InPlay

                    "FINISHED" ->
                        Finished

                    "PAUSED" ->
                        Paused

                    _ ->
                        Pending
            )


teamDecoder : Json.Decoder Team
teamDecoder =
    Json.succeed Team
        |> required "name" Json.string
        |> required "tla" Json.string


fixtureStage : Fixture -> String
fixtureStage fixture =
    case fixture.stage of
        GroupStage ->
            "Alagrupimäng"

        RoundOf32 ->
            "32 parema voor"

        RoundOf16 ->
            "Kaheksandikfinaal"

        QuarterFinals ->
            "Veerandfinaal"

        SemiFinals ->
            "Poolfinaal"

        Final ->
            "Finaal"

        Unknown ->
            ""


dateFormatter : Zone -> Posix -> String
dateFormatter =
    DateFormat.format
        [ DateFormat.dayOfMonthFixed
        , DateFormat.text "."
        , DateFormat.monthFixed
        , DateFormat.text "."
        , DateFormat.yearNumber
        , DateFormat.text " "
        , DateFormat.hourMilitaryFixed
        , DateFormat.text ":"
        , DateFormat.minuteFixed
        ]
