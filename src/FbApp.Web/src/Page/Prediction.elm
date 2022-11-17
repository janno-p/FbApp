module Page.Prediction exposing (Model, Msg, init, subscriptions, toSession, update, view)

import Api.Endpoint as Endpoint
import Html exposing (Html, a, button, div, h1, img, p, text)
import Html.Attributes exposing (class, classList, height, href, src, style, title)
import Html.Events exposing (onClick)
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (required)
import Session exposing (Session)



-- MODEL


type alias Model =
    { session : Session
    , fixturePredictions : Maybe FixturePredictions
    , stage : PredictionStage
    }


type PredictionStage
    = Initial
    | GroupStage
    | RoundOf32Stage
    | RoundOf16Stage
    | QuarterFinalsStage
    | SemiFinalsStage
    | FinalsStage
    | TopScorersStage


type alias FixturePredictions =
    { competitionId : String
    , teams : List Team
    , fixtures : List Fixture
    , groups : List Group
    }


type alias Team =
    { id : Int
    , name : String
    , flagUrl : String
    }


type alias Fixture =
    { id : Int
    , homeTeamId : Int
    , awayTeamId : Int
    }


type alias Group =
    { name : String
    , teamIds : List Int
    }


init : Session -> ( Model, Cmd Msg )
init session =
    ( { session = session, fixturePredictions = Nothing, stage = Initial }
    , Cmd.none
    )



-- VIEW


view : Model -> { title : String, content : Html Msg }
view model =
    let
        content =
            case model.stage of
                Initial ->
                    viewNote

                GroupStage ->
                    viewGroupStage model

                RoundOf32Stage ->
                    viewRoundOf32Stage model

                RoundOf16Stage ->
                    viewRoundOf16Stage model

                QuarterFinalsStage ->
                    viewQuarterFinalsStage model

                SemiFinalsStage ->
                    viewSemiFinalsStage model

                FinalsStage ->
                    viewFinalsStage model

                TopScorersStage ->
                    viewTopScorersStage model
    in
    { title = "Ennustamine"
    , content = div [] content
    }


viewNote : List (Html Msg)
viewNote =
    let
        introduction =
            """
            Ajavahemikus 20. novembrist 18. detsembrini toimuvad Kataris 2022. aasta jalgpalli
            maailmameistrivõistlused. Lisaks rahvusmeeskondade mõõduvõtmistele pakub antud veebileht
            omavahelist võistlusmomenti ka tugitoolisportlastele tulemuste ennustamise näol.
            """

        deadline =
            """
            Oma eelistusi saad valida ja muuta kuni avamänguni 20. novembril kell 18:00. Pärast seda on
            võimalik sama veebilehe vahendusel jälgida, kuidas tegelikud tulemused kujunevad ning kui täpselt
            need Sinu või teiste ennustustega kokku langevad.
            """

        motivation =
            """
            Auhinnaks lühiajaline au ja kuulsus.
            """
    in
    [ div []
        [ p [] [ text introduction ]
        , p [] [ text deadline ]
        , p [] [ text motivation ]
        , button [ onClick SetGroupStage ] [ text "Tee oma ennustused »" ]
        ]
    ]


findTeam : Model -> Int -> Maybe Team
findTeam model teamId =
    model.fixturePredictions
        |> Maybe.andThen (\predictions -> predictions.teams |> List.filter (\val -> val.id == teamId) |> List.head)


viewGroupStage : Model -> List (Html Msg)
viewGroupStage model =
    let
        fixturesContent =
            case model.fixturePredictions of
                Just fixtures ->
                    fixtures.fixtures
                        |> List.map
                            (\fixture ->
                                let
                                    ht =
                                        findTeam model fixture.homeTeamId

                                    at =
                                        findTeam model fixture.awayTeamId
                                in
                                div [ class "flex flex-row" ]
                                    [ viewTeamSelection ht
                                    , viewTeamSelection at
                                    ]
                            )

                Nothing ->
                    []
    in
    [ h1 [] [ text "Alagrupimängud" ]
    , p [] [ text "Millise tulemusega lõppeb mäng?" ]
    , div [ class "grid grid-cols-4 gap-4" ] fixturesContent
    , button [ onClick SetRoundOf32Stage ] [ text "Jätka alagrupist edasipääsejate ennustamisega »" ]
    ]


viewRoundOf32Stage : Model -> List (Html Msg)
viewRoundOf32Stage model =
    [ h1 [] [ text "Alagrupist edasipääsejad" ]
    , p [] [ text "Millised meeskonnad jätkavad väljalangemismängudega?" ]
    ]
        ++ viewPlayOffSelection 16 model
        ++ [ button [ onClick SetRoundOf16Stage ] [ text "Jätka veerandfinalistide ennustamisega »" ] ]


viewRoundOf16Stage : Model -> List (Html Msg)
viewRoundOf16Stage model =
    [ h1 [] [ text "Veerandfinalistid" ]
    , p [] [ text "Millised meeskonnad jõuavad veerandfinaalidesse?" ]
    ]
        ++ viewPlayOffSelection 8 model
        ++ [ button [ onClick SetQuarterFinalsStage ] [ text "Jätka poolfinalistide ennustamisega »" ] ]


viewQuarterFinalsStage : Model -> List (Html Msg)
viewQuarterFinalsStage model =
    [ h1 [] [ text "Poolfinalistid" ]
    , p [] [ text "Millised meeskonnad jõuavad poolfinaalidesse?" ]
    ]
        ++ viewPlayOffSelection 4 model
        ++ [ button [ onClick SetSemiFinalsStage ] [ text "Jätka finalistide ennustamisega »" ] ]


viewSemiFinalsStage : Model -> List (Html Msg)
viewSemiFinalsStage model =
    [ h1 [] [ text "Finalistid" ]
    , p [] [ text "Millised on kaks meeskonda, kelle vahel selgitatakse turniiri võitja?" ]
    ]
        ++ viewPlayOffSelection 2 model
        ++ [ button [ onClick SetFinalsStage ] [ text "Jätka võitja ennustamisega »" ] ]


viewFinalsStage : Model -> List (Html Msg)
viewFinalsStage model =
    [ h1 [] [ text "Maailmameister" ]
    , p [] [ text "Milline meeskond on uus maailmameister?" ]
    ]
        ++ viewPlayOffSelection 2 model
        ++ [ button [ onClick SetTopScorersStage ] [ text "Jätka suurimate väravaküttide ennustamisega »" ] ]


viewTopScorersStage : Model -> List (Html Msg)
viewTopScorersStage model =
    [ h1 [] [ text "Suurimad väravakütid" ]
    , p [] [ text "Kes on kolm resultatiivsemat väravalööjat?" ]
    ]
        ++ viewPlayOffSelection 2 model
        ++ [ button [ onClick SetTopScorersStage ] [ text "Registreeri oma ennustus" ] ]


viewPlayOffSelection : Int -> Model -> List (Html Msg)
viewPlayOffSelection maxSelection model =
    case model.fixturePredictions of
        Just predictions ->
            predictions.teams
                |> List.map
                    (\team ->
                        div []
                            [ img [ src team.flagUrl, height 16 ] []
                            , text team.name
                            ]
                    )

        Nothing ->
            []


viewTeamSelection : Maybe Team -> Html Msg
viewTeamSelection maybeTeam =
    case maybeTeam of
        Just team ->
            div [ class "h-16 w-16 rounded-full bg-red-200 drop-shadow-md shadow-green-200 cursor-pointer flex justify-items-center", title team.name ]
                [ div [ style "background-size" "4rem 4rem", style "background-image" ("url('" ++ team.flagUrl ++ "')"), class "bg-center bg-no-repeat bg-contain rounded-md w-16 h-16" ]
                    []
                ]

        Nothing ->
            p [] [ text "X" ]



-- UPDATE


type Msg
    = SessionUpdated Session
    | SetGroupStage
    | GotFixturePredictions (Result Http.Error FixturePredictions)
    | SetRoundOf32Stage
    | SetRoundOf16Stage
    | SetQuarterFinalsStage
    | SetSemiFinalsStage
    | SetFinalsStage
    | SetTopScorersStage


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        SessionUpdated session ->
            ( { model | session = session }, Cmd.none )

        SetGroupStage ->
            ( model, getFixturePredictions )

        SetRoundOf32Stage ->
            ( { model | stage = RoundOf32Stage }, Cmd.none )

        SetRoundOf16Stage ->
            ( { model | stage = RoundOf16Stage }, Cmd.none )

        SetQuarterFinalsStage ->
            ( { model | stage = QuarterFinalsStage }, Cmd.none )

        SetSemiFinalsStage ->
            ( { model | stage = SemiFinalsStage }, Cmd.none )

        SetFinalsStage ->
            ( { model | stage = FinalsStage }, Cmd.none )

        SetTopScorersStage ->
            ( { model | stage = TopScorersStage }, Cmd.none )

        GotFixturePredictions (Ok fixturePredictions) ->
            ( { model | stage = GroupStage, fixturePredictions = Just fixturePredictions }, Cmd.none )

        GotFixturePredictions _ ->
            ( model, Cmd.none )


getFixturePredictions : Cmd Msg
getFixturePredictions =
    Endpoint.request
        Endpoint.fixturePredictions
        (Http.expectJson GotFixturePredictions fixturePredictionsDecoder)
        Endpoint.defaultEndpointConfig


fixturePredictionsDecoder : Json.Decoder FixturePredictions
fixturePredictionsDecoder =
    Json.succeed FixturePredictions
        |> required "competitionId" Json.string
        |> required "teams" (Json.list teamDecoder)
        |> required "fixtures" (Json.list fixtureDecoder)
        |> required "groups" (Json.list groupDecoder)


teamDecoder : Json.Decoder Team
teamDecoder =
    Json.succeed Team
        |> required "id" Json.int
        |> required "name" Json.string
        |> required "flagUrl" Json.string


fixtureDecoder : Json.Decoder Fixture
fixtureDecoder =
    Json.succeed Fixture
        |> required "id" Json.int
        |> required "awayTeamId" Json.int
        |> required "homeTeamId" Json.int


groupDecoder : Json.Decoder Group
groupDecoder =
    Json.succeed Group
        |> required "name" Json.string
        |> required "teamIds" (Json.list Json.int)



-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
    Sub.none



-- EXPORT


toSession : Model -> Session
toSession model =
    model.session
