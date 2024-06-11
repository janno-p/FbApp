module Page.Prediction exposing (Model, Msg, init, subscriptions, update, view)

import Api.Endpoint as Endpoint exposing (competitionInfo, defaultEndpointConfig)
import Html exposing (Html, button, div, h1, h2, img, input, label, p, pre, span, table, tbody, td, text, th, thead, tr)
import Html.Attributes exposing (checked, class, disabled, for, id, placeholder, scope, src, title, type_, value)
import Html.Events exposing (onClick, onInput)
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (optional, required)
import Json.Encode as Encode
import Session exposing (Session)



-- MODEL


type alias Model =
    { competitionInfo : Maybe CompetitionInfo
    , stage : PredictionStage
    , fixturePredictions : List FixturePrediction
    , groupPredictions : List GroupPrediction
    , top16 : List Int
    , top8 : List Int
    , top4 : List Int
    , top2 : List Int
    , top1 : List Int
    , topScorers : List PlayerPrediction
    , playerPredictions : List PlayerPrediction
    , playerFilter : String
    , selectedPositions : List String
    , selectedCountries : List Int
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
    | Done
    | FailedToSave String


type alias CompetitionInfo =
    { competitionId : String
    , teams : List Team
    , fixtures : List Fixture
    , groups : List Group
    , players : List Player
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


type alias Player =
    { id : Int
    , name : String
    , position : String
    , teamId : Int
    }


type FixtureResult
    = HomeWin
    | Tie
    | AwayWin


type alias FixturePrediction =
    { fixtureId : Int
    , result : Maybe FixtureResult
    , homeTeam : Team
    , awayTeam : Team
    }


type alias GroupPrediction =
    { name : String
    , teams : List Team
    }


type alias PlayerPrediction =
    { playerId : Int
    , name : String
    , position : String
    , team : Team
    }


init : Session -> ( Model, Cmd Msg )
init session =
    ( { competitionInfo = Nothing
      , stage = Initial
      , fixturePredictions = []
      , groupPredictions = []
      , top16 = []
      , top8 = []
      , top4 = []
      , top2 = []
      , top1 = []
      , topScorers = []
      , playerPredictions = []
      , playerFilter = ""
      , selectedPositions = []
      , selectedCountries = []
      }
    , checkExisting session
    )



-- VIEW


view : Session -> Model -> { title : String, content : Html Msg }
view session model =
    let
        content =
            case model.stage of
                Initial ->
                    viewNote

                GroupStage ->
                    viewGroupStage model

                RoundOf32Stage ->
                    viewPlayOffStage
                        model
                        model.top16
                        "Alagrupist edasipääsejad"
                        "Millised meeskonnad jätkavad väljalangemismängudega?"
                        "Jätka veerandfinalistide ennustamisega"
                        16
                        SetRoundOf16Stage

                RoundOf16Stage ->
                    viewPlayOffStage
                        model
                        model.top8
                        "Veerandfinalistid"
                        "Millised meeskonnad jõuavad veerandfinaalidesse?"
                        "Jätka poolfinalistide ennustamisega"
                        8
                        SetQuarterFinalsStage

                QuarterFinalsStage ->
                    viewPlayOffStage
                        model
                        model.top4
                        "Poolfinalistid"
                        "Millised meeskonnad jõuavad poolfinaalidesse?"
                        "Jätka finalistide ennustamisega"
                        4
                        SetSemiFinalsStage

                SemiFinalsStage ->
                    viewPlayOffStage
                        model
                        model.top2
                        "Finalistid"
                        "Millised on kaks meeskonda, kelle vahel selgitatakse turniiri võitja?"
                        "Jätka võitja ennustamisega"
                        2
                        SetFinalsStage

                FinalsStage ->
                    viewPlayOffStage
                        model
                        model.top1
                        "Maailmameister"
                        "Milline meeskond on uus maailmameister?"
                        "Jätka suurimate väravaküttide ennustamisega"
                        1
                        SetTopScorersStage

                TopScorersStage ->
                    viewTopScorersStage session model

                Done ->
                    viewDone

                FailedToSave reason ->
                    viewFailure session reason
    in
    { title = "Ennustamine"
    , content = div [ class "p-8" ] content
    }


viewFailure : Session -> String -> List (Html Msg)
viewFailure session reason =
    [ div []
        [ p [] [ text "Viga salvestamisel! Ennustuse edastamine ei õnnestunud." ]
        , pre [] [ text reason ]
        , div [ class "flex flex-row-reverse" ]
            [ viewButton (SavePrediction session) "Proovi uuesti salvestada" "mdi-replay" False ]
        ]
    ]


viewDone : List (Html Msg)
viewDone =
    [ p [] [ text "Tehtud! Oled juba ennustanud." ] ]


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
        , div [ class "flex flex-row-reverse" ]
            [ viewButton SetGroupStage "Tee oma ennustused" "mdi-chevron-double-right" False ]
        ]
    ]


findTeam : List Team -> Int -> Maybe Team
findTeam teams teamId =
    teams |> List.filter (\val -> val.id == teamId) |> List.head


viewGroupStage : Model -> List (Html Msg)
viewGroupStage model =
    let
        selectionClass fixture result =
            if fixture.result == Just result then
                class "bg-green-200 hover:bg-green-400"

            else if fixture.result == Nothing then
                class "bg-gray-200 hover:bg-gray-400"

            else
                class "bg-red-200 hover:bg-red-400"

        fixturesContent =
            model.fixturePredictions
                |> List.map
                    (\fixture ->
                        div [ class "flex flex-row gap-1" ]
                            [ button
                                [ class "rounded-md basis-1/3 p-2 flex flex-row items-center gap-1"
                                , selectionClass fixture HomeWin
                                , onClick (ToggleFixtureResult fixture.fixtureId HomeWin)
                                ]
                                [ img [ src fixture.homeTeam.flagUrl, class "h-4" ] []
                                , text fixture.homeTeam.name
                                ]
                            , button
                                [ class "rounded-md basis-1/3"
                                , selectionClass fixture Tie
                                , onClick (ToggleFixtureResult fixture.fixtureId Tie)
                                ]
                                [ text "Jääb viiki" ]
                            , button
                                [ class "rounded-md basis-1/3 p-2 flex flex-row-reverse items-center gap-1"
                                , selectionClass fixture AwayWin
                                , onClick (ToggleFixtureResult fixture.fixtureId AwayWin)
                                ]
                                [ img [ src fixture.awayTeam.flagUrl, class "h-4" ] []
                                , text fixture.awayTeam.name
                                ]
                            ]
                    )

        disableNextStep =
            model.fixturePredictions |> List.any (\f -> f.result == Nothing)
    in
    [ h1 [] [ text "Alagrupimängud" ]
    , p [] [ text "Kes võidab mängu?" ]
    , div [ class "grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-4 gap-x-8 gap-y-4 my-8" ] fixturesContent
    , div [ class "flex flex-row-reverse" ]
        [ viewButton SetRoundOf32Stage "Jätka alagrupist edasipääsejate ennustamisega" "mdi-chevron-double-right" disableNextStep
        ]
    ]


viewButton : Msg -> String -> String -> Bool -> Html Msg
viewButton msg label icon isDisabled =
    let
        colorClass =
            if isDisabled then
                class "border-gray-400 bg-gray-200 text-gray-400"

            else
                class "border-blue-600 bg-blue-200 hover:bg-blue-400 drop-shadow-md cursor-pointer"
    in
    button
        [ class "rounded-md p-2 border"
        , colorClass
        , onClick msg
        , disabled isDisabled
        ]
        [ div [ class "items-center flex flex-row gap-1" ]
            [ text label
            , span [ class "mdi", class icon ] []
            ]
        ]


viewPlayOffStage : Model -> List Int -> String -> String -> String -> Int -> Msg -> List (Html Msg)
viewPlayOffStage model selection title subTitle btnText count msg =
    let
        disableNextStep =
            List.length selection /= count
    in
    [ h1 [] [ text title ]
    , p [] [ text subTitle ]
    , div [ class "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-8 gap-4 my-8" ] (viewPlayOffSelection count model)
    , div [ class "flex flex-row-reverse" ]
        [ viewButton msg btnText "mdi-chevron-double-right" disableNextStep
        ]
    ]


viewTopScorersStage : Session -> Model -> List (Html Msg)
viewTopScorersStage session model =
    [ h1 [] [ text "Suurim väravakütt" ]
    , p [] [ text "Vali kolm kandidaati resultatiivseima väravalööja tiitlile?" ]
    ]
        ++ (model.topScorers
                |> List.map (\x -> p [] [ text x.name ])
           )
        ++ [ div [ class "flex flex-row-reverse" ]
                [ viewButton (SavePrediction session) "Registreeri oma ennustus" "mdi-chevron-double-right" (List.length model.topScorers /= 3) ]
           , viewPlayerTable model
           ]


viewSearch : Model -> Html Msg
viewSearch model =
    div [ class "bg-white dark:bg-gray-900" ]
        [ label [ for "table-search", class "sr-only" ] [ text "Otsi" ]
        , div [ class "relative mt-1" ]
            [ div [ class "flex absolute inset-y-0 left-0 items-center pl-3 pointer-events-none" ] []
            , input [ value model.playerFilter, onInput SetPlayerNameFilter, type_ "text", id "table-search", class "block p-2 pl-10 w-80 text-sm text-gray-900 bg-gray-50 rounded-lg border border-gray-300 focus:ring-blue-500 focus:border-blue-500 dark:bg-gray-700 dark:border-gray-600 dark:placeholder-gray-400 dark:text-white dark:focus:ring-blue-500 dark:focus:border-blue-500", placeholder "Otsi nime järgi" ] []
            ]
        ]


unique : List a -> List a
unique =
    List.foldl
        (\a uniques ->
            if List.member a uniques then
                uniques

            else
                uniques ++ [ a ]
        )
        []


viewPositionFilter : Model -> Html Msg
viewPositionFilter model =
    let
        positions =
            model.playerPredictions
                |> List.map (\x -> x.position)
                |> unique
                |> List.map
                    (\x ->
                        label [ class "cursor-pointer flex items-center gap-1" ]
                            [ input
                                [ checked (List.member x model.selectedPositions)
                                , onClick (TogglePositionFilter x)
                                , type_ "checkbox"
                                , class "cursor-pointer w-4 h-4 text-blue-600 bg-gray-100 rounded border-gray-300 focus:ring-blue-500 dark:focus:ring-blue-600 dark:ring-offset-gray-800 focus:ring-2 dark:bg-gray-700 dark:border-gray-600"
                                ]
                                []
                            , text x
                            ]
                    )
    in
    div [ class "border border-gray-300 rounded-md flex flex-wrap gap-3 p-4" ]
        positions


viewCountryFilter : Model -> Html Msg
viewCountryFilter model =
    let
        countries =
            model.competitionInfo
                |> Maybe.map (\a -> a.teams)
                |> Maybe.withDefault []
                |> List.map
                    (\a ->
                        label [ class "cursor-pointer flex items-center gap-1" ]
                            [ input
                                [ checked (List.member a.id model.selectedCountries)
                                , onClick (ToggleCountryFilter a.id)
                                , type_ "checkbox"
                                , class "cursor-pointer w-4 h-4 text-blue-600 bg-gray-100 rounded border-gray-300 focus:ring-blue-500 dark:focus:ring-blue-600 dark:ring-offset-gray-800 focus:ring-2 dark:bg-gray-700 dark:border-gray-600"
                                ]
                                []
                            , img [ src a.flagUrl, class "h-4" ] []
                            , text a.name
                            ]
                    )
    in
    div [ class "border border-gray-300 rounded-md flex flex-wrap gap-3 p-4" ]
        countries


viewPlayerTable : Model -> Html Msg
viewPlayerTable model =
    div [ class "overflow-x-auto relative shadow-md sm:rounded-lg space-y-4" ]
        [ viewSearch model
        , viewPositionFilter model
        , viewCountryFilter model
        , table [ class "w-full text-sm text-left text-gray-500 dark:text-gray-400" ]
            [ thead [ class "text-xs text-gray-700 uppercase bg-gray-50 dark:bg-gray-700 dark:text-gray-400" ]
                [ tr []
                    [ th [ scope "col", class "p-4" ] []
                    , th [ scope "col", class "py-3 px-6" ]
                        [ text "Nimi" ]
                    , th [ scope "col", class "py-3 px-6" ]
                        [ text "Positsioon" ]
                    , th [ scope "col", class "py-3 px-6" ]
                        [ text "Meeskond" ]
                    ]
                ]
            , tbody []
                (model
                    |> filterPlayers
                    |> List.map
                        (\player ->
                            tr [ class "bg-white border-b dark:bg-gray-800 dark:border-gray-700 hover:bg-gray-50 dark:hover:bg-gray-600" ]
                                [ td [ class "p-4 w-4" ]
                                    [ div [ class "flex items-center" ]
                                        [ input
                                            [ checked (List.member player model.topScorers)
                                            , onClick (ToggleScorer player)
                                            , id "checkbox-table-search-1"
                                            , type_ "checkbox"
                                            , class "w-4 h-4 text-blue-600 bg-gray-100 rounded border-gray-300 focus:ring-blue-500 dark:focus:ring-blue-600 dark:ring-offset-gray-800 focus:ring-2 dark:bg-gray-700 dark:border-gray-600"
                                            , disabled (List.length model.topScorers == 3 && not (List.member player model.topScorers))
                                            ]
                                            []
                                        , label [ for "checkbox-table-search-1", class "sr-only" ] [ text "checkbox" ]
                                        ]
                                    ]
                                , th [ scope "row", class "py-4 px-6 font-medium text-gray-900 whitespace-nowrap dark:text-white" ]
                                    [ text player.name ]
                                , td [ class "py-4 px-6" ]
                                    [ text player.position ]
                                , td [ class "py-4 px-6" ]
                                    [ div [ class "p-2 flex flex-row items-center gap-1" ]
                                        [ img [ src player.team.flagUrl, class "h-4" ] []
                                        , text player.team.name
                                        ]
                                    ]
                                ]
                        )
                )
            ]
        ]


viewGroupSelection : List Int -> Int -> GroupPrediction -> Html Msg
viewGroupSelection selectedTeams count grp =
    let
        selectionClass teamId =
            if List.member teamId selectedTeams then
                class "bg-green-200 hover:bg-green-400 cursor-pointer"

            else if List.length selectedTeams /= count then
                class "bg-gray-200 hover:bg-gray-400 cursor-pointer"

            else
                class "bg-red-200 cursor-default"

        viewBtn team =
            button
                [ class "rounded-md p-2 flex flex-row items-center gap-1"
                , selectionClass team.id
                , onClick (ToggleQualifier team.id)
                ]
                [ img [ src team.flagUrl, class "h-4" ] []
                , text team.name
                ]
    in
    div [ class "flex flex-col" ]
        (h2 [] [ text (grp.name ++ " alagrupp") ]
            :: (grp.teams
                    |> List.map viewBtn
               )
        )


viewPlayOffSelection : Int -> Model -> List (Html Msg)
viewPlayOffSelection maxSelection model =
    let
        selectedTeams =
            case maxSelection of
                16 ->
                    model.top16

                8 ->
                    model.top8

                4 ->
                    model.top4

                2 ->
                    model.top2

                1 ->
                    model.top1

                _ ->
                    []
    in
    model.groupPredictions |> List.map (viewGroupSelection selectedTeams maxSelection)



-- UPDATE


type Msg
    = SetGroupStage
    | GotCompetitionInfo (Result Http.Error CompetitionInfo)
    | SetRoundOf32Stage
    | SetRoundOf16Stage
    | SetQuarterFinalsStage
    | SetSemiFinalsStage
    | SetFinalsStage
    | SetTopScorersStage
    | ToggleFixtureResult Int FixtureResult
    | ToggleQualifier Int
    | ToggleScorer PlayerPrediction
    | SetPlayerNameFilter String
    | SavePrediction Session
    | PredictionSaved (Result ErrorDetailed ( Http.Metadata, String ))
    | TogglePositionFilter String
    | ToggleCountryFilter Int
    | CheckedExisting (Result Http.Error (Maybe String))


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        CheckedExisting (Ok (Just _)) ->
            ( { model | stage = Done }, Cmd.none )

        CheckedExisting _ ->
            ( model, getCompetitionInfo )

        SetGroupStage ->
            ( { model | stage = GroupStage }, Cmd.none )

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

        GotCompetitionInfo (Ok competitionInfo) ->
            ( preparePredictions { model | competitionInfo = Just competitionInfo }, Cmd.none )

        GotCompetitionInfo _ ->
            ( model, Cmd.none )

        ToggleFixtureResult fixtureId fixtureResult ->
            ( { model | fixturePredictions = model.fixturePredictions |> List.map (updateFixtureResult fixtureId fixtureResult) }
            , Cmd.none
            )

        ToggleQualifier teamId ->
            case model.stage of
                RoundOf32Stage ->
                    ( { model | top16 = updateQualifier 16 model.top16 teamId }, Cmd.none )

                RoundOf16Stage ->
                    ( { model | top8 = updateQualifier 8 model.top8 teamId }, Cmd.none )

                QuarterFinalsStage ->
                    ( { model | top4 = updateQualifier 4 model.top4 teamId }, Cmd.none )

                SemiFinalsStage ->
                    ( { model | top2 = updateQualifier 2 model.top2 teamId }, Cmd.none )

                FinalsStage ->
                    ( { model | top1 = updateQualifier 1 model.top1 teamId }, Cmd.none )

                _ ->
                    ( model, Cmd.none )

        ToggleScorer playerId ->
            ( { model | topScorers = toggleListItem playerId model.topScorers }
            , Cmd.none
            )

        SetPlayerNameFilter val ->
            ( { model | playerFilter = String.toLower val }
            , Cmd.none
            )

        SavePrediction session ->
            ( model, savePrediction session model )

        PredictionSaved (Err (BadStatus _ body)) ->
            ( { model | stage = FailedToSave body }, Cmd.none )

        PredictionSaved (Err _) ->
            ( { model | stage = FailedToSave "Viga salvestamisel" }, Cmd.none )

        PredictionSaved _ ->
            ( { model | stage = Done }, Cmd.none )

        TogglePositionFilter val ->
            ( { model | selectedPositions = toggleListItem val model.selectedPositions }, Cmd.none )

        ToggleCountryFilter val ->
            ( { model | selectedCountries = toggleListItem val model.selectedCountries }, Cmd.none )


toggleListItem : a -> List a -> List a
toggleListItem item items =
    if items |> List.any ((==) item) then
        items |> List.filter ((/=) item)

    else
        item :: items


checkExisting : Session -> Cmd Msg
checkExisting session =
    Endpoint.request
        Endpoint.prediction
        (Http.expectJson CheckedExisting (Json.nullable Json.string))
        { defaultEndpointConfig | headers = Endpoint.useToken session }


type ErrorDetailed
    = BadUrl String
    | Timeout
    | NetworkError
    | BadStatus Http.Metadata String


convertResponseString : Http.Response String -> Result ErrorDetailed ( Http.Metadata, String )
convertResponseString httpResponse =
    case httpResponse of
        Http.BadUrl_ url ->
            Err (BadUrl url)

        Http.Timeout_ ->
            Err Timeout

        Http.NetworkError_ ->
            Err NetworkError

        Http.BadStatus_ metadata body ->
            Err (BadStatus metadata body)

        Http.GoodStatus_ metadata body ->
            Ok ( metadata, body )


savePrediction : Session -> Model -> Cmd Msg
savePrediction session model =
    let
        config =
            Endpoint.defaultEndpointConfig
    in
    Endpoint.request
        Endpoint.savePrediction
        (Http.expectStringResponse PredictionSaved convertResponseString)
        { config
            | body = Http.jsonBody (predictionEncoder model)
            , method = "POST"
            , headers = Endpoint.useToken session
        }


updateQualifier : Int -> List Int -> Int -> List Int
updateQualifier count selectedTeams teamId =
    if List.member teamId selectedTeams then
        selectedTeams |> List.filter ((/=) teamId)

    else if List.length selectedTeams == count then
        selectedTeams

    else
        teamId :: selectedTeams


updateFixtureResult : Int -> FixtureResult -> FixturePrediction -> FixturePrediction
updateFixtureResult fixtureId fixtureResult fixture =
    if fixture.fixtureId == fixtureId then
        { fixture
            | result =
                if fixture.result == Just fixtureResult then
                    Nothing

                else
                    Just fixtureResult
        }

    else
        fixture


filterByPosition : Model -> List PlayerPrediction -> List PlayerPrediction
filterByPosition model players =
    if List.isEmpty model.selectedPositions then
        players

    else
        players |> List.filter (\a -> List.member a.position model.selectedPositions)


filterByCountry : Model -> List PlayerPrediction -> List PlayerPrediction
filterByCountry model players =
    if List.isEmpty model.selectedCountries then
        players

    else
        players |> List.filter (\a -> List.member a.team.id model.selectedCountries)


filterPlayers : Model -> List PlayerPrediction
filterPlayers model =
    getPlayerPredictions model
        |> List.filter (\p -> String.contains model.playerFilter (String.toLower p.name))
        |> filterByPosition model
        |> filterByCountry model


getPlayerPredictions : Model -> List PlayerPrediction
getPlayerPredictions model =
    model.competitionInfo
        |> Maybe.map (\comp -> comp.players |> List.filterMap (mapPlayer comp))
        |> Maybe.withDefault []
        |> List.sortBy (\x -> x.name)


preparePredictions : Model -> Model
preparePredictions model =
    let
        fixturePredictions =
            model.competitionInfo
                |> Maybe.map (\comp -> comp.fixtures |> List.filterMap (mapFixture comp))
                |> Maybe.withDefault []

        groupPredictions =
            model.competitionInfo
                |> Maybe.map (\comp -> comp.groups |> List.filterMap (mapGroup comp))
                |> Maybe.withDefault []
    in
    { model
        | fixturePredictions = fixturePredictions
        , groupPredictions = groupPredictions
        , playerPredictions = getPlayerPredictions model
    }


mapPlayer : CompetitionInfo -> Player -> Maybe PlayerPrediction
mapPlayer competitionInfo playerDto =
    findTeam competitionInfo.teams playerDto.teamId
        |> Maybe.map (\team -> { playerId = playerDto.id, name = playerDto.name, position = playerDto.position, team = team })


mapGroup : CompetitionInfo -> Group -> Maybe GroupPrediction
mapGroup competitionInfo groupDto =
    let
        teams =
            groupDto.teamIds |> List.filterMap (findTeam competitionInfo.teams)
    in
    if List.length teams == 4 then
        Just { name = groupDto.name |> String.replace "GROUP_" "", teams = teams }

    else
        Nothing


mapFixture : CompetitionInfo -> Fixture -> Maybe FixturePrediction
mapFixture competitionInfo fixtureDto =
    let
        homeTeam =
            findTeam competitionInfo.teams fixtureDto.homeTeamId

        awayTeam =
            findTeam competitionInfo.teams fixtureDto.awayTeamId
    in
    case ( homeTeam, awayTeam ) of
        ( Just team1, Just team2 ) ->
            Just { fixtureId = fixtureDto.id, homeTeam = team1, awayTeam = team2, result = Nothing }

        _ ->
            Nothing


getCompetitionInfo : Cmd Msg
getCompetitionInfo =
    Endpoint.request
        Endpoint.competitionInfo
        (Http.expectJson GotCompetitionInfo competitionInfoDecoder)
        Endpoint.defaultEndpointConfig


competitionInfoDecoder : Json.Decoder CompetitionInfo
competitionInfoDecoder =
    Json.succeed CompetitionInfo
        |> required "competitionId" Json.string
        |> required "teams" (Json.list teamDecoder)
        |> required "fixtures" (Json.list fixtureDecoder)
        |> required "groups" (Json.list groupDecoder)
        |> required "players" (Json.list playerDecoder)


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
        |> required "homeTeamId" Json.int
        |> required "awayTeamId" Json.int


groupDecoder : Json.Decoder Group
groupDecoder =
    Json.succeed Group
        |> required "name" Json.string
        |> required "teamIds" (Json.list Json.int)


playerDecoder : Json.Decoder Player
playerDecoder =
    Json.succeed Player
        |> required "id" Json.int
        |> required "name" Json.string
        |> optional "position"
            (Json.map
                (\v ->
                    case v of
                        "" ->
                            "Unknown"

                        "Midfielder" ->
                            "Midfield"

                        _ ->
                            v
                )
                Json.string
            )
            "Unknown"
        |> required "teamId" Json.int


predictionEncoder : Model -> Encode.Value
predictionEncoder model =
    Encode.object
        [ ( "competitionId", Encode.string (model.competitionInfo |> Maybe.map (\x -> x.competitionId) |> Maybe.withDefault "") )
        , ( "fixtures", Encode.list fixtureResultEncoder model.fixturePredictions )
        , ( "qualifiers", qualifiersEncoder model )
        , ( "winner", Encode.int (model.top1 |> List.head |> Maybe.withDefault 0) )
        , ( "topScorers", Encode.list Encode.int (model.topScorers |> List.map (\x -> x.playerId)) )
        ]


qualifiersEncoder : Model -> Encode.Value
qualifiersEncoder model =
    Encode.object
        [ ( "roundOf16", Encode.list Encode.int model.top16 )
        , ( "roundOf8", Encode.list Encode.int model.top8 )
        , ( "roundOf4", Encode.list Encode.int model.top4 )
        , ( "roundOf2", Encode.list Encode.int model.top2 )
        ]


fixtureResultEncoder : FixturePrediction -> Encode.Value
fixtureResultEncoder fixture =
    let
        result =
            case fixture.result of
                Just HomeWin ->
                    "HOME"

                Just Tie ->
                    "TIE"

                Just AwayWin ->
                    "AWAY"

                Nothing ->
                    "NOTHING"
    in
    Encode.object
        [ ( "id", Encode.int fixture.fixtureId )
        , ( "result", Encode.string result )
        ]



-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
    Sub.none
