module Page.Fixture exposing (Model, Msg, init, update, view)

import Api.Endpoint as Endpoint exposing (defaultEndpointConfig, fixture)
import Browser.Navigation as Nav
import DateFormat
import Html exposing (Attribute, Html, button, div, img, span, text)
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
    , session : Session
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


type alias Team =
    { name : String
    , flagUrl : String
    }


type alias FixtureResultPrediction =
    { name : String
    , result : FixtureResult
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
    }


init : Session -> Maybe String -> ( Model, Cmd Msg )
init session fixtureId =
    let
        model =
            { fixtureId = fixtureId
            , session = session
            , fixture = Nothing
            , timeZone = utc
            }
    in
    ( model
    , Cmd.batch
        [ loadFixture model
        , Task.attempt SetZone Time.here
        ]
    )


view : Model -> { title : String, content : Html Msg }
view model =
    let
        content =
            div []
                [ viewResultsTabs FixtureTab
                , div [ class "sm:rounded-md sm:border border-gray-200 sm:w-[40rem] sm:mx-auto mt-4 sm:mt-8 sm:shadow-lg" ]
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
    in
    div [ class "flex flex-row flex-nowrap border-b border-gray-200" ]
        [ button
            ([ title "Eelmine mäng", disabled (previousFixtureId == Nothing), class "grow-0" ]
                ++ ([ previousFixtureId |> Maybe.map (LoadFixture >> onClick) ] |> List.filterMap identity)
            )
            [ span [ class "mdi mdi-arrow-left" ] [] ]
        , div [ class "grow text-center" ] [ text <| fixtureTitle fixture ]
        , button
            ([ title "Järgmine mäng", disabled (nextFixtureId == Nothing), class "grow-0" ]
                ++ ([ nextFixtureId |> Maybe.map (LoadFixture >> onClick) ] |> List.filterMap identity)
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
    in
    div []
        [ div [ class "flex flex-row flex-nowrap border-b border-gray-200" ]
            [ viewTeam fixture.homeTeam
            , viewScore zone fixture
            , viewTeam fixture.awayTeam
            ]
        , div [ class "flex flex-col" ]
            (List.map (viewResultPrediction fixture expectedResult) fixture.resultPredictions)
        ]


viewTeam : Team -> Html Msg
viewTeam team =
    div [ class "grow-0 w-32 self-center content-center" ]
        [ img [ class "h-8 mx-auto", src team.flagUrl, title team.name ] []
        , div [ class "text-center" ] [ text team.name ]
        ]


viewScore : Zone -> Fixture -> Html Msg
viewScore zone fixture =
    div [ class "grow" ]
        (div
            [ class "flex flex-col text-center" ]
            [ span [] [ text <| fixtureStage fixture ]
            , span [] [ text (homeGoals fixture ++ " : " ++ awayGoals fixture) ]
            , span [] [ text <| dateFormatter zone fixture.date ]
            ]
            :: (case fixture.penalties of
                    Just ( home, away ) ->
                        [ text ("(" ++ String.fromInt home ++ " : " ++ String.fromInt away ++ ")") ]

                    Nothing ->
                        []
               )
        )


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
    div []
        [ viewResultIcon expectedResult fixtureResult
        , text fixtureResult.name
        , predictionText
        ]


viewResultIcon : Maybe FixtureResult -> FixtureResultPrediction -> Html Msg
viewResultIcon expectedResult fixtureResult =
    case expectedResult of
        Just result ->
            if result == fixtureResult.result then
                span [ class "mdi mdi-check" ] []

            else
                span [ class "mdi mdi-close" ] []

        Nothing ->
            span [ class "mdi mdi-minus" ] []


viewPlayOffPrediction : Html Msg
viewPlayOffPrediction =
    div [] []


type Msg
    = FixtureLoaded (Result Http.Error Fixture)
    | SetZone (Result () Zone)
    | LoadFixture String


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
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
            ( updated, Route.replaceUrl (Session.navKey model.session) (Route.Fixture (Just fixtureId)) )


loadFixture : Model -> Cmd Msg
loadFixture model =
    let
        endpoint =
            model.fixtureId
                |> Maybe.map Endpoint.fixture
                |> Maybe.withDefault Endpoint.defaultFixture
    in
    Endpoint.request
        endpoint
        (Http.expectJson FixtureLoaded fixtureDecoder)
        { defaultEndpointConfig | headers = Endpoint.useToken model.session }


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
                    "ROUND_OF_16" ->
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
            "Väljakukkumismäng"

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
