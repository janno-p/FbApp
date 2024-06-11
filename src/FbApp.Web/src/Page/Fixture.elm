module Page.Fixture exposing (Model, Msg, init, update, view)

import Api.Endpoint as Endpoint exposing (defaultEndpointConfig, fixture)
import DateFormat
import Html exposing (Html, button, div, img, span, table, tbody, td, text, tr)
import Html.Attributes exposing (class, disabled, src, title)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (required)
import Page exposing (PageTab(..), viewResultsTabs)
import Route
import Session exposing (Session)
import Task
import Time exposing (Posix, Zone, utc)
import Tuple exposing (first, second)
import Url exposing (Protocol(..))


type alias Model =
    { fixtureId : Maybe String
    , fixture : Maybe Fixture
    , timeZone : Zone
    }


type FixtureStage
    = GroupStage
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
    , flagUrl : String
    }


type alias FixtureResultPrediction =
    { name : String
    , result : FixtureResult
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
                , div [ class "sm:rounded-md sm:border border-gray-200 sm:w-[40rem] sm:mx-auto mt-2 sm:mt-8 sm:shadow-lg" ]
                    [ viewFixtureHeader model.fixture
                    , case model.fixture of
                        Just fixture ->
                            viewFixture model.timeZone fixture

                        Nothing ->
                            div [ class "mt-8 text-center" ] [ text "Laadimine ..." ]
                    ]
                ]
    in
    { title = "Mängude tulemused"
    , content = content
    }


viewFixtureHeader : Maybe Fixture -> Html Msg
viewFixtureHeader fixture =
    let
        previousFixtureId =
            Maybe.andThen (\x -> x.previousFixtureId) fixture

        nextFixtureId =
            Maybe.andThen (\x -> x.nextFixtureId) fixture

        buttonStateAttrs refId =
            case refId of
                Nothing ->
                    [ disabled True, class "text-gray-200 cursor-default" ]

                Just id ->
                    [ onClick (LoadFixture id), class "cursor-pointer hover:bg-gray-100 shadow" ]
    in
    div [ class "flex flex-row flex-nowrap border-b border-gray-200 p-4" ]
        [ button
            ([ title "Eelmine mäng", class "grow-0 w-8 h-8 rounded-full border border-gray-100" ]
                ++ buttonStateAttrs previousFixtureId
            )
            [ span [ class "mdi mdi-arrow-left" ] [] ]
        , div [ class "grow text-center" ] [ text <| fixtureTitle fixture ]
        , button
            ([ title "Järgmine mäng", class "grow-0 w-8 h-8 rounded-full border border-gray-100" ]
                ++ buttonStateAttrs nextFixtureId
            )
            [ span [ class "mdi mdi-arrow-right" ] [] ]
        ]


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
        [ div [ class "flex flex-row flex-nowrap border-b border-gray-200 py-2" ]
            [ viewTeam fixture.homeTeam
            , viewScore zone fixture
            , viewTeam fixture.awayTeam
            ]
        , table [ class "w-full mt-4 mb-8" ]
            [ tbody [] (resultPredictions ++ qualifierPredictions) ]
        ]


viewTeam : Team -> Html Msg
viewTeam team =
    div [ class "grow-0 w-32 self-center content-center" ]
        [ img [ class "h-8 mx-auto", src team.flagUrl, title team.name ] []
        , div [ class "text-center" ] [ text team.name ]
        ]


viewScore : Zone -> Fixture -> Html Msg
viewScore zone fixture =
    let
        penalties =
            case fixture.penalties of
                Just ( home, away ) ->
                    [ div [ class "text-xs font-semibold uppercase" ] [ text ("(pen " ++ String.fromInt home ++ " : " ++ String.fromInt away ++ ")") ] ]

                Nothing ->
                    []
    in
    div [ class "grow" ]
        [ div
            [ class "flex flex-col text-center" ]
            (div [ class "text-3xl font-bold" ] [ text (homeGoals fixture ++ " : " ++ awayGoals fixture) ]
                :: penalties
                ++ [ div [ class "text-xs mt-2" ] [ text <| dateFormatter zone fixture.date ]
                   , div [ class "text-xs uppercase" ] [ text <| fixtureStage fixture ]
                   ]
            )
        ]


viewResultPrediction : Fixture -> Maybe FixtureResult -> FixtureResultPrediction -> Html Msg
viewResultPrediction fixture expectedResult fixtureResult =
    let
        predictionText =
            case fixtureResult.result of
                HomeWin ->
                    text fixture.homeTeam.name

                AwayWin ->
                    text fixture.awayTeam.name

                Tie ->
                    text "Viik"
    in
    tr [ class "border-b" ]
        [ td [ class "px-4 w-14" ] [ viewResultIcon expectedResult fixtureResult.result ]
        , td [ class "capitalize" ] [ text fixtureResult.name ]
        , td [] [ predictionText ]
        ]


viewResultIcon : Maybe FixtureResult -> FixtureResult -> Html Msg
viewResultIcon expectedResult predictedResult =
    case expectedResult of
        Just result ->
            if result == predictedResult then
                span [ class "mdi mdi-check text-green-500 text-2xl" ] []

            else
                span [ class "mdi mdi-close text-red-500 text-2xl" ] []

        Nothing ->
            span [ class "mdi mdi-minus text-gray-200 text-2xl" ] []


qualifierIconClass : Bool -> Html.Attribute Msg
qualifierIconClass expectQualifies =
    if expectQualifies then
        class "mdi mdi-check"

    else
        class "mdi mdi-close"


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


fixtureResultDecoder : Json.Decoder FixtureResult
fixtureResultDecoder =
    Json.string
        |> Json.map
            (\value ->
                -- TODO : Check why are predicted result values in reverse
                case value of
                    "HomeWin" ->
                        AwayWin

                    "AwayWin" ->
                        HomeWin

                    _ ->
                        Tie
            )


fixtureStageDecoder : Json.Decoder FixtureStage
fixtureStageDecoder =
    Json.string
        |> Json.map
            (\value ->
                case value of
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
        |> required "flagUrl" Json.string


fixtureStage : Fixture -> String
fixtureStage fixture =
    case fixture.stage of
        GroupStage ->
            "Alagrupimäng"

        RoundOf16 ->
            "Kohamäng"

        QuarterFinals ->
            "Veerandfinaal"

        SemiFinals ->
            "Poolfinaal"

        Final ->
            "Finaal"

        Unknown ->
            ""


fixtureTitle : Maybe Fixture -> String
fixtureTitle fixture =
    case Maybe.map (\x -> x.status) fixture of
        Just InPlay ->
            "Käimasolev mäng"

        Just Finished ->
            "Lõppenud mäng"

        Just Paused ->
            "Käimasolev mäng (vaheaeg)"

        Just Pending ->
            "Toimumata mäng"

        Nothing ->
            "Mängu andmete laadimine"


homeGoals : Fixture -> String
homeGoals fixture =
    fixture.fullTime |> Maybe.map (first >> String.fromInt) |> Maybe.withDefault "-"


awayGoals : Fixture -> String
awayGoals fixture =
    fixture.fullTime |> Maybe.map (second >> String.fromInt) |> Maybe.withDefault "-"


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
