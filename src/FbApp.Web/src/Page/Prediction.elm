module Page.Prediction exposing (Model, Msg, init, subscriptions, update, view)

import Api.Endpoint as Endpoint exposing (competitionInfo, defaultEndpointConfig)
import Dict exposing (Dict)
import Html exposing (Html, button, div, h1, input, label, p, pre, span, table, tbody, td, text, th, thead, tr)
import Html.Attributes exposing (checked, class, disabled, for, id, placeholder, scope, title, type_, value)
import Html.Events exposing (onClick, onInput)
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (optional, required)
import Json.Encode as Encode
import Random
import Session exposing (Session)
import Team exposing (flagClass)



-- MODEL


type alias Model =
    { competitionInfo : Maybe CompetitionInfo
    , stage : PredictionStage
    , fixturePredictions : List GroupFixturePrediction
    , groupPredictions : List GroupPrediction
    , topScorers : List PlayerPrediction
    , playerPredictions : List PlayerPrediction
    , playerFilter : String
    , selectedPositions : List String
    , selectedCountries : List Int
    , thirds : List TeamRanking
    , roundOf32 : List KnockoutMatch
    , roundOf16 : List KnockoutMatch
    , quarterFinals : List KnockoutMatch
    , semiFinals : List KnockoutMatch
    , final : List KnockoutMatch
    }


type alias KnockoutMatch =
    { team1 : Team
    , team2 : Team
    , winner : Maybe Team
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
    , groups : List Group
    , players : List Player
    }


type alias Team =
    { id : Int
    , name : String
    , tla : String
    }


type alias Fixture =
    { id : Int
    , homeTeamId : Int
    , awayTeamId : Int
    }


type alias Group =
    { name : String
    , teamIds : List Int
    , fixtures : List Fixture
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
    , groupName : String
    , confident : Bool
    }


type RankingStatus
    = Loose
    | Fixed
    | UserDefined


type alias TeamRanking =
    { status : Maybe RankingStatus
    , team : Team
    , points : Int
    , groupName : String
    }


type alias GroupFixturePrediction =
    { groupName : String
    , fixtures : List FixturePrediction
    , rankings : List TeamRanking
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
      , topScorers = []
      , playerPredictions = []
      , playerFilter = ""
      , selectedPositions = []
      , selectedCountries = []
      , thirds = []
      , roundOf32 = []
      , roundOf16 = []
      , quarterFinals = []
      , semiFinals = []
      , final = []
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
                    viewGroupStage session model

                RoundOf32Stage ->
                    viewKnockoutRound
                        model.roundOf32
                        "Kaheksandikfinalistid"
                        "Millised meeskonnad jõuavad 16 parema hulka?"
                        "Jätka veerandfinalistide ennustamisega"
                        SetRoundOf16Stage

                RoundOf16Stage ->
                    viewKnockoutRound
                        model.roundOf16
                        "Veerandfinalistid"
                        "Millised meeskonnad jõuavad veerandfinaalidesse?"
                        "Jätka poolfinalistide ennustamisega"
                        SetQuarterFinalsStage

                QuarterFinalsStage ->
                    viewKnockoutRound
                        model.quarterFinals
                        "Poolfinalistid"
                        "Millised meeskonnad jõuavad poolfinaalidesse?"
                        "Jätka finalistide ennustamisega"
                        SetSemiFinalsStage

                SemiFinalsStage ->
                    viewKnockoutRound
                        model.semiFinals
                        "Finalistid"
                        "Millised on kaks meeskonda, kelle vahel selgitatakse turniiri võitja?"
                        "Jätka võitja ennustamisega"
                        SetFinalsStage

                FinalsStage ->
                    viewKnockoutRound
                        model.final
                        "Maailmameister"
                        "Milline meeskond on järgmine maailmameister?"
                        "Jätka suurimate väravaküttide ennustamisega"
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
            [ viewButton (SavePrediction session) "Proovi uuesti salvestada" "icon-[mdi--replay]" False ]
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
            Ajavahemikus 11. juunist kuni 19. juulini toimuvad Põhja-Ameerikas 2026. aasta jalgpalli
            maailmameistrivõistlused. Lisaks rahvusmeeskondade mõõduvõtmistele pakub antud veebileht
            omavahelist võistlusmomenti ka tugitoolisportlastele tulemuste ennustamise näol.
            """

        deadline =
            """
            Oma eelistusi saad valida ja muuta kuni avamänguni 11. juuni kell 22:00. Pärast seda on
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
            [ viewButton SetGroupStage "Tee oma ennustused" "icon-[mdi--chevron-double-right]" False ]
        ]
    ]


findTeam : List Team -> Int -> Maybe Team
findTeam teams teamId =
    teams |> List.filter (\val -> val.id == teamId) |> List.head


calculateGroupTable : List FixturePrediction -> List ( Team, Int )
calculateGroupTable fixtures =
    let
        updateTable ( team, points ) table =
            table
                |> Dict.update team.id
                    (\val ->
                        case val of
                            Just ( _, pts ) ->
                                Just ( team, pts + points )

                            Nothing ->
                                Just ( team, points )
                    )
    in
    fixtures
        |> List.foldl
            (\f table ->
                case f.result of
                    Nothing ->
                        table |> updateTable ( f.homeTeam, 0 ) |> updateTable ( f.awayTeam, 0 )

                    Just HomeWin ->
                        table |> updateTable ( f.homeTeam, 3 ) |> updateTable ( f.awayTeam, 0 )

                    Just Tie ->
                        table |> updateTable ( f.homeTeam, 1 ) |> updateTable ( f.awayTeam, 1 )

                    Just AwayWin ->
                        table |> updateTable ( f.homeTeam, 0 ) |> updateTable ( f.awayTeam, 3 )
            )
            Dict.empty
        |> Dict.values
        |> List.sortBy (\( _, v ) -> -v)


viewRankControls : TeamRanking -> Maybe Int -> Maybe Int -> Int -> String -> List (Html Msg)
viewRankControls ranking prevPts nextPts rank groupName =
    if ranking.status == Just Loose || ranking.status == Just UserDefined then
        (if prevPts == Just ranking.points then
            [ span [ class "flex-none icon-[mdi--arrow-up-thick] place-self-center cursor-pointer", onClick (SetUserRanking groupName rank (rank - 1)) ] [] ]

         else
            []
        )
            ++ (if nextPts == Just ranking.points then
                    [ span [ class "flex-none icon-[mdi--arrow-down-thick] place-self-center cursor-pointer", onClick (SetUserRanking groupName rank (rank + 1)) ] [] ]

                else
                    []
               )
            ++ (if ranking.status == Just Loose then
                    [ span [ class "flex-none icon-[mdi--checkbox-marked-circle-outline] place-self-center cursor-pointer", onClick (SetUserRanking groupName rank rank) ] [] ]

                else
                    []
               )

    else
        []


viewGroupTable : GroupFixturePrediction -> Html Msg
viewGroupTable group =
    let
        prev i =
            if i == 0 then
                Nothing

            else
                group.rankings |> List.drop (i - 1) |> List.head |> Maybe.map .points

        next i =
            if (i - 1) == List.length group.rankings then
                Nothing

            else
                group.rankings |> List.drop (i + 1) |> List.head |> Maybe.map .points
    in
    div [ class "mt-8 mb-8" ]
        [ h1
            []
            [ text "Ennustatav tabeliseis" ]
        , div []
            [ viewTodo [ text "Otsusta, milline on meeskondade lõplik järjestus alagrupis" ] (group.rankings |> List.all (\r -> r.status == Just Fixed || r.status == Just UserDefined))
            , div [ class "border border-accent" ]
                (group.rankings
                    |> List.indexedMap
                        (\i ranking ->
                            div
                                (class "flex flex-row gap-2 py-2"
                                    :: (if i < 2 && (ranking.status == Just Fixed || ranking.status == Just UserDefined) then
                                            [ class "bg-green-200 text-green-900" ]

                                        else if i == 2 && (ranking.status == Just Fixed || ranking.status == Just UserDefined) then
                                            [ class "bg-amber-200 text-amber-900" ]

                                        else if i == 3 && (ranking.status == Just Fixed || ranking.status == Just UserDefined) then
                                            [ class "bg-red-200 text-red-900" ]

                                        else
                                            [ class "bg-slate-100 text-slate-600" ]
                                       )
                                )
                                ([ span [ class "font-mono flex-none px-2" ] [ text (String.fromInt (i + 1) ++ ".") ]
                                 , span (flagClass ranking.team.tla ++ [ class "size-6 flex-none" ]) []
                                 , span [ class "capitalize font-mono grow" ] [ text ranking.team.tla ]
                                 ]
                                    ++ viewRankControls ranking (prev i) (next i) i group.groupName
                                    ++ [ span [ class "flex-none px-2" ] [ text (String.fromInt ranking.points ++ "pts") ]
                                       ]
                                )
                        )
                )
            ]
        ]


viewThirdRankControls : TeamRanking -> Maybe Int -> Maybe Int -> Int -> List (Html Msg)
viewThirdRankControls ranking prevPts nextPts rank =
    if ranking.status == Just Loose || ranking.status == Just UserDefined then
        (if prevPts == Just ranking.points then
            [ span [ class "flex-none icon-[mdi--arrow-up-thick] place-self-center cursor-pointer", onClick (SetThirdsUserRanking rank (rank - 1)) ] [] ]

         else
            []
        )
            ++ (if nextPts == Just ranking.points then
                    [ span [ class "flex-none icon-[mdi--arrow-down-thick] place-self-center cursor-pointer", onClick (SetThirdsUserRanking rank (rank + 1)) ] [] ]

                else
                    []
               )
            ++ (if ranking.status == Just Loose then
                    [ span [ class "flex-none icon-[mdi--checkbox-marked-circle-outline] place-self-center cursor-pointer", onClick (SetThirdsUserRanking rank rank) ] [] ]

                else
                    []
               )

    else
        []


updateThirds : List GroupFixturePrediction -> List TeamRanking
updateThirds groups =
    let
        table =
            groups
                |> List.filterMap
                    (\x ->
                        case x.rankings of
                            [ _, _, ranking, _ ] ->
                                Just ranking

                            _ ->
                                Nothing
                    )
                |> List.sortBy (\x -> -x.points)

        ( pts8th, pts9th ) =
            case table |> List.drop 7 of
                p8 :: p9 :: _ ->
                    ( p8.points, p9.points )

                _ ->
                    ( 0, 0 )

        allResults =
            groups
                |> List.all
                    (\g -> g.fixtures |> List.all (\x -> x.result /= Nothing))
    in
    table
        |> List.indexedMap
            (\i ranking ->
                { ranking
                    | status =
                        if not allResults then
                            Nothing

                        else if ranking.points > pts8th || (i < 8 && pts8th /= pts9th) then
                            Just Fixed

                        else if pts8th == ranking.points then
                            Just Loose

                        else
                            Just Fixed
                }
            )


viewThirdPlaceRankings : List TeamRanking -> Html Msg
viewThirdPlaceRankings thirds =
    let
        prev i =
            if i == 0 then
                Nothing

            else
                thirds |> List.drop (i - 1) |> List.head |> Maybe.map .points

        next i =
            if (i - 1) == List.length thirds then
                Nothing

            else
                thirds |> List.drop (i + 1) |> List.head |> Maybe.map .points
    in
    div [ class "flex flex-col items-center mb-8" ]
        [ h1 [] [ text "Kolmanda koha meeskonnad" ]
        , div [ class "w-100" ]
            [ viewTodo [ text "Otsusta, millised 8 meeskonda pääsevad edasi alagrupi kolmandana" ] (thirds |> List.all (\r -> r.status == Just Fixed || r.status == Just UserDefined))
            , div [ class "border border-accent" ]
                (thirds
                    |> List.indexedMap
                        (\i ranking ->
                            div
                                (class "flex flex-row gap-2 py-2"
                                    :: (if i < 8 && (ranking.status == Just Fixed || ranking.status == Just UserDefined) then
                                            [ class "bg-green-200 text-green-900" ]

                                        else if i > 7 && (ranking.status == Just Fixed || ranking.status == Just UserDefined) then
                                            [ class "bg-red-200 text-red-900" ]

                                        else
                                            [ class "bg-slate-100 text-slate-600" ]
                                       )
                                )
                                ([ span [ class "font-mono flex-none px-2 w-10 text-right" ] [ text (String.fromInt (i + 1) ++ ".") ]
                                 , span (flagClass ranking.team.tla ++ [ class "size-6 flex-none" ]) []
                                 , span [ class "capitalize font-mono grow" ] [ text ranking.team.tla ]
                                 ]
                                    ++ viewThirdRankControls ranking (prev i) (next i) i
                                    ++ [ span [ class "flex-none px-2" ] [ text (String.fromInt ranking.points ++ "pts") ]
                                       ]
                                )
                        )
                )
            ]
        ]


viewTodo : List (Html Msg) -> Bool -> Html Msg
viewTodo content isCompleted =
    let
        ( borderClass, bgClass, ( iconClass, textClass ) ) =
            if isCompleted then
                ( class "border-emerald-200"
                , class "bg-emerald-50"
                , ( class "icon-[mdi--check-box] text-emerald-500"
                  , class "text-emerald-800"
                  )
                )

            else
                ( class "border-amber-200"
                , class "bg-amber-50"
                , ( class "icon-[mdi--check-box-outline-blank] text-amber-500"
                  , class "text-amber-800"
                  )
                )
    in
    div [ class "rounded-lg border p-4 my-2", borderClass, bgClass ]
        [ div [ class "flex items-start gap-3", textClass ]
            [ span [ class "flex-none size-6", iconClass ] []
            , div []
                [ p [ class "mt-1 text-sm grow" ] content
                ]
            ]
        ]


viewGroupStage : Session -> Model -> List (Html Msg)
viewGroupStage session model =
    let
        selectionClass fixture result =
            if fixture.result == Just result then
                class "bg-green-200 hover:bg-green-400"

            else if fixture.result == Nothing then
                class "bg-gray-200 hover:bg-gray-400"

            else
                class "bg-red-200 hover:bg-red-400"

        fixturesContent groupName fixture =
            div [ class "flex flex-row gap-1" ]
                [ button
                    [ class "rounded-md basis-1/3 p-2 flex flex-row items-center gap-1 cursor-pointer"
                    , selectionClass fixture HomeWin
                    , onClick (ToggleFixtureResult groupName fixture.fixtureId HomeWin)
                    , title fixture.homeTeam.name
                    ]
                    [ span (flagClass fixture.homeTeam.tla ++ [ class "size-6 flex-none" ]) []
                    , span [ class "capitalize grow font-mono" ] [ text fixture.homeTeam.tla ]
                    ]
                , button
                    [ class "rounded-md basis-1/3 cursor-pointer"
                    , selectionClass fixture Tie
                    , onClick (ToggleFixtureResult groupName fixture.fixtureId Tie)
                    ]
                    [ text "Jääb viiki" ]
                , button
                    [ class "rounded-md basis-1/3 p-2 flex flex-row-reverse items-center gap-1 cursor-pointer"
                    , selectionClass fixture AwayWin
                    , onClick (ToggleFixtureResult groupName fixture.fixtureId AwayWin)
                    , title fixture.awayTeam.name
                    ]
                    [ span (flagClass fixture.awayTeam.tla ++ [ class "size-6 flex-none" ]) []
                    , span [ class "capitalize grow font-mono" ] [ text fixture.awayTeam.tla ]
                    ]
                , div
                    [ class "flex flex-row items-center" ]
                    [ span
                        ([ class "text-lg icon-[mdi--verified] hover:opacity-100 cursor-pointer bg-violet-700 border-violet-200"
                         , title "Topeltpanus"
                         , onClick (SetConfidenceBooster groupName fixture)
                         ]
                            ++ (if fixture.confident then
                                    [ class "opacity-100" ]

                                else
                                    [ class "opacity-25" ]
                               )
                        )
                        []
                    ]
                ]

        groupsContent =
            model.fixturePredictions
                |> List.map
                    (\groupFixture ->
                        div [ class "w-100" ]
                            (h1 [] [ text (groupFixture.groupName ++ " alagrupp") ]
                                :: viewTodo [ text "Vali tulemused kõigile alagrupimängudele" ] (groupFixture.fixtures |> List.all (\f -> f.result /= Nothing))
                                :: viewTodo [ text "Märgi topeltpanus ", span [ class "icon-[mdi--verified]" ] [], text " mängule, mille tulemuses oled kõige kindlam" ] (groupFixture.fixtures |> List.any (\f -> f.confident))
                                :: (groupFixture.fixtures
                                        |> List.map (fixturesContent groupFixture.groupName)
                                   )
                                ++ [ viewGroupTable groupFixture ]
                            )
                    )

        allFixturesPredicted =
            model.fixturePredictions |> List.all (\g -> g.fixtures |> List.all (\f -> f.result /= Nothing))

        allBoostersSet =
            model.fixturePredictions |> List.all (\g -> g.fixtures |> List.any (\f -> f.confident))

        allRankingsResolved =
            model.fixturePredictions |> List.all (\g -> g.rankings |> List.all (\r -> r.status == Just Fixed || r.status == Just UserDefined))

        thirdPlaceRankingsResolved =
            model.thirds |> List.all (\r -> r.status == Just Fixed || r.status == Just UserDefined)

        disableNextStep =
            not (allFixturesPredicted && allBoostersSet && allRankingsResolved && thirdPlaceRankingsResolved)
    in
    [ h1 [] [ text "Alagrupimängud" ]
    , p [] [ text "Kes võidab mängu?" ]

    --, viewButton RandomizeGroupStage "Rändom!" "icon-[mdi--dice]" False
    , div [ class "flex flex-col gap-y-4 my-8 items-center" ] groupsContent
    , viewThirdPlaceRankings model.thirds
    , div [ class "flex flex-row-reverse" ]
        [ viewButton (SetRoundOf32Stage session) "Jätka väljakukkumismängude ennustamisega" "icon-[mdi--chevron-double-right]" disableNextStep
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
            , span [ class icon ] []
            ]
        ]


viewKnockoutRound : List KnockoutMatch -> String -> String -> String -> Msg -> List (Html Msg)
viewKnockoutRound matches titleText subTitle btnText msg =
    let
        disableNextStep =
            matches |> List.any (\m -> m.winner == Nothing)

        selectionClass team winner =
            if Just team == winner then
                class "bg-green-200 hover:bg-green-400"

            else if winner == Nothing then
                class "bg-gray-200 hover:bg-gray-400"

            else
                class "bg-red-200 hover:bg-red-400"
    in
    [ h1 [] [ text titleText ]
    , p [] [ text subTitle ]
    , div [ class "grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-8 gap-4 my-8" ]
        (matches
            |> List.map
                (\m ->
                    div []
                        [ div []
                            [ button
                                [ class "rounded-md basis-1/3 p-2 flex flex-row items-center gap-1 cursor-pointer"
                                , selectionClass m.team1 m.winner
                                , onClick (ToggleKnockoutWinner m m.team1)
                                , title m.team1.name
                                ]
                                [ span (flagClass m.team1.tla ++ [ class "size-6 flex-none" ]) []
                                , span [ class "capitalize grow font-mono" ] [ text m.team1.tla ]
                                ]
                            ]
                        , div
                            []
                            [ button
                                [ class "rounded-md basis-1/3 p-2 flex flex-row items-center gap-1 cursor-pointer"
                                , selectionClass m.team2 m.winner
                                , onClick (ToggleKnockoutWinner m m.team2)
                                , title m.team2.name
                                ]
                                [ span (flagClass m.team2.tla ++ [ class "size-6 flex-none" ]) []
                                , span [ class "capitalize grow font-mono" ] [ text m.team2.tla ]
                                ]
                            ]
                        ]
                )
        )
    , div [ class "flex flex-row-reverse" ]
        [ viewButton msg btnText "icon-[mdi--chevron-double-right]" disableNextStep
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
                [ viewButton (SavePrediction session) "Registreeri oma ennustus" "icon-[mdi--chevron-double-right]" (List.length model.topScorers /= 3) ]
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
                            , span (flagClass a.tla ++ [ class "h-4" ]) []
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
                                        [ span (flagClass player.team.tla ++ [ class "h-4" ]) []
                                        , text player.team.name
                                        ]
                                    ]
                                ]
                        )
                )
            ]
        ]



-- UPDATE


type Msg
    = SetGroupStage
    | GotCompetitionInfo (Result Http.Error CompetitionInfo)
    | SetRoundOf32Stage Session
    | SetRoundOf16Stage
    | SetQuarterFinalsStage
    | SetSemiFinalsStage
    | SetFinalsStage
    | SetTopScorersStage
    | ToggleFixtureResult String Int FixtureResult
    | ToggleQualifier Int
    | ToggleScorer PlayerPrediction
    | SetPlayerNameFilter String
    | SavePrediction Session
    | PredictionSaved (Result ErrorDetailed ( Http.Metadata, String ))
    | TogglePositionFilter String
    | ToggleCountryFilter Int
    | CheckedExisting (Result Http.Error (Maybe String))
    | RandomizeGroupStage
    | GotRandomGroupResults (List FixtureResult)
    | SetUserRanking String Int Int
    | SetThirdsUserRanking Int Int
    | SetConfidenceBooster String FixturePrediction
    | GotThirdPlaceMatchups (Result Http.Error (List String))
    | ToggleKnockoutWinner KnockoutMatch Team


knockoutAdvance : List KnockoutMatch -> List KnockoutMatch
knockoutAdvance round =
    case round of
        a :: b :: xs ->
            case ( a.winner, b.winner ) of
                ( Just tm1, Just tm2 ) ->
                    { team1 = tm1, team2 = tm2, winner = Nothing } :: knockoutAdvance xs

                _ ->
                    []

        _ ->
            []


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        CheckedExisting (Ok (Just _)) ->
            ( { model | stage = Done }, Cmd.none )

        CheckedExisting _ ->
            ( model, getCompetitionInfo )

        SetGroupStage ->
            ( { model | stage = GroupStage }, Cmd.none )

        SetRoundOf32Stage session ->
            ( model, getThirdPlaceMatchups session (model.thirds |> List.take 8 |> List.map (\r -> r.groupName)) )

        SetRoundOf16Stage ->
            ( { model
                | stage = RoundOf16Stage
                , roundOf16 = knockoutAdvance model.roundOf32
              }
            , Cmd.none
            )

        SetQuarterFinalsStage ->
            ( { model
                | stage = QuarterFinalsStage
                , quarterFinals = knockoutAdvance model.roundOf16
              }
            , Cmd.none
            )

        SetSemiFinalsStage ->
            ( { model
                | stage = SemiFinalsStage
                , semiFinals = knockoutAdvance model.quarterFinals
              }
            , Cmd.none
            )

        SetFinalsStage ->
            ( { model
                | stage = FinalsStage
                , final = knockoutAdvance model.semiFinals
              }
            , Cmd.none
            )

        SetTopScorersStage ->
            ( { model | stage = TopScorersStage }, Cmd.none )

        GotCompetitionInfo (Ok competitionInfo) ->
            ( preparePredictions { model | competitionInfo = Just competitionInfo }, Cmd.none )

        GotCompetitionInfo _ ->
            ( model, Cmd.none )

        ToggleFixtureResult groupName fixtureId fixtureResult ->
            let
                fixturePredictions =
                    model.fixturePredictions
                        |> List.map
                            (\g ->
                                if g.groupName == groupName then
                                    updateFixtureResult fixtureId fixtureResult g

                                else
                                    g
                            )
            in
            ( { model
                | fixturePredictions = fixturePredictions
                , thirds = updateThirds fixturePredictions
              }
            , Cmd.none
            )

        ToggleQualifier teamId ->
            case model.stage of
                RoundOf32Stage ->
                    ( { model
                        | roundOf32 =
                            model.roundOf32
                                |> List.map
                                    (\m ->
                                        if m.team1.id == teamId then
                                            { m | winner = Just m.team1 }

                                        else if m.team2.id == teamId then
                                            { m | winner = Just m.team2 }

                                        else
                                            m
                                    )
                      }
                    , Cmd.none
                    )

                RoundOf16Stage ->
                    ( { model
                        | roundOf16 =
                            model.roundOf16
                                |> List.map
                                    (\m ->
                                        if m.team1.id == teamId then
                                            { m | winner = Just m.team1 }

                                        else if m.team2.id == teamId then
                                            { m | winner = Just m.team2 }

                                        else
                                            m
                                    )
                      }
                    , Cmd.none
                    )

                QuarterFinalsStage ->
                    ( { model
                        | quarterFinals =
                            model.quarterFinals
                                |> List.map
                                    (\m ->
                                        if m.team1.id == teamId then
                                            { m | winner = Just m.team1 }

                                        else if m.team2.id == teamId then
                                            { m | winner = Just m.team2 }

                                        else
                                            m
                                    )
                      }
                    , Cmd.none
                    )

                SemiFinalsStage ->
                    ( { model
                        | semiFinals =
                            model.semiFinals
                                |> List.map
                                    (\m ->
                                        if m.team1.id == teamId then
                                            { m | winner = Just m.team1 }

                                        else if m.team2.id == teamId then
                                            { m | winner = Just m.team2 }

                                        else
                                            m
                                    )
                      }
                    , Cmd.none
                    )

                FinalsStage ->
                    ( { model
                        | final =
                            model.final
                                |> List.map
                                    (\m ->
                                        if m.team1.id == teamId then
                                            { m | winner = Just m.team1 }

                                        else if m.team2.id == teamId then
                                            { m | winner = Just m.team2 }

                                        else
                                            m
                                    )
                      }
                    , Cmd.none
                    )

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

        RandomizeGroupStage ->
            ( model
            , Random.generate GotRandomGroupResults (Random.list (12 * 6) (Random.uniform HomeWin [ Tie, AwayWin ]))
            )

        GotRandomGroupResults results ->
            let
                fixturePredictions =
                    randomizeFixturePredictions model.fixturePredictions results
            in
            ( { model
                | fixturePredictions = fixturePredictions
                , thirds = updateThirds fixturePredictions
              }
            , Cmd.none
            )

        SetUserRanking groupName oldRank newRank ->
            let
                fixturePredictions =
                    setUserRank model.fixturePredictions groupName oldRank newRank
            in
            ( { model
                | fixturePredictions = fixturePredictions
                , thirds = updateThirds fixturePredictions
              }
            , Cmd.none
            )

        SetThirdsUserRanking oldRank newRank ->
            ( { model | thirds = setThirdsUserRank model.thirds oldRank newRank }, Cmd.none )

        SetConfidenceBooster groupName fixture ->
            ( { model
                | fixturePredictions =
                    model.fixturePredictions
                        |> List.map
                            (\g ->
                                if g.groupName == groupName then
                                    { g
                                        | fixtures =
                                            g.fixtures
                                                |> List.map
                                                    (\f ->
                                                        if f == fixture then
                                                            { f | confident = True }

                                                        else
                                                            { f | confident = False }
                                                    )
                                    }

                                else
                                    g
                            )
              }
            , Cmd.none
            )

        GotThirdPlaceMatchups (Err _) ->
            ( { model | stage = FailedToSave "Viga salvestamisel" }, Cmd.none )

        GotThirdPlaceMatchups (Ok matchups) ->
            ( { model
                | stage = RoundOf32Stage
                , roundOf32 = prepareRoundOf32 model matchups
              }
            , Cmd.none
            )

        ToggleKnockoutWinner knockoutMatch winner ->
            ( { model
                | roundOf32 =
                    if model.stage == RoundOf32Stage then
                        model.roundOf32
                            |> List.map
                                (\m ->
                                    if m == knockoutMatch then
                                        { m | winner = Just winner }

                                    else
                                        m
                                )

                    else
                        model.roundOf32
                , roundOf16 =
                    if model.stage == RoundOf16Stage then
                        model.roundOf16
                            |> List.map
                                (\m ->
                                    if m == knockoutMatch then
                                        { m | winner = Just winner }

                                    else
                                        m
                                )

                    else
                        model.roundOf16
                , quarterFinals =
                    if model.stage == QuarterFinalsStage then
                        model.quarterFinals
                            |> List.map
                                (\m ->
                                    if m == knockoutMatch then
                                        { m | winner = Just winner }

                                    else
                                        m
                                )

                    else
                        model.quarterFinals
                , semiFinals =
                    if model.stage == SemiFinalsStage then
                        model.semiFinals
                            |> List.map
                                (\m ->
                                    if m == knockoutMatch then
                                        { m | winner = Just winner }

                                    else
                                        m
                                )

                    else
                        model.semiFinals
                , final =
                    if model.stage == FinalsStage then
                        model.final
                            |> List.map
                                (\m ->
                                    if m == knockoutMatch then
                                        { m | winner = Just winner }

                                    else
                                        m
                                )

                    else
                        model.final
              }
            , Cmd.none
            )


getTeam : Model -> Int -> String -> Maybe Team
getTeam model position groupName =
    model.fixturePredictions
        |> List.filter (\g -> g.groupName == groupName)
        |> List.map (\g -> g.rankings)
        |> List.concat
        |> List.drop position
        |> List.map (\r -> r.team)
        |> List.head


getThird : Model -> List String -> Int -> Maybe Team
getThird model matchups position =
    let
        groupName =
            matchups
                |> List.drop position
                |> List.head
    in
    groupName |> Maybe.andThen (\g -> getTeam model 2 g)


prepareRoundOf32 : Model -> List String -> List KnockoutMatch
prepareRoundOf32 model matchups =
    let
        g1 =
            getTeam model 0

        g2 =
            getTeam model 1

        g3 =
            getThird model matchups

        mk f1 f2 =
            case ( f1 (), f2 () ) of
                ( Just t1, Just t2 ) ->
                    Just { team1 = t1, team2 = t2, winner = Nothing }

                _ ->
                    Nothing
    in
    [ mk (\() -> g1 "E") (\() -> g3 3)
    , mk (\() -> g1 "I") (\() -> g3 5)
    , mk (\() -> g2 "A") (\() -> g2 "B")
    , mk (\() -> g1 "F") (\() -> g2 "C")
    , mk (\() -> g2 "K") (\() -> g2 "L")
    , mk (\() -> g1 "H") (\() -> g2 "J")
    , mk (\() -> g1 "D") (\() -> g3 2)
    , mk (\() -> g1 "G") (\() -> g3 4)
    , mk (\() -> g1 "C") (\() -> g2 "F")
    , mk (\() -> g2 "E") (\() -> g2 "I")
    , mk (\() -> g1 "A") (\() -> g3 0)
    , mk (\() -> g1 "L") (\() -> g3 7)
    , mk (\() -> g1 "J") (\() -> g2 "H")
    , mk (\() -> g2 "D") (\() -> g2 "G")
    , mk (\() -> g1 "B") (\() -> g3 1)
    , mk (\() -> g1 "K") (\() -> g3 6)
    ]
        |> List.filterMap identity


setThirdsUserRank : List TeamRanking -> Int -> Int -> List TeamRanking
setThirdsUserRank rankings oldRank newRank =
    let
        mi =
            min oldRank newRank

        ma =
            max oldRank newRank
    in
    if mi == ma then
        rankings
            |> List.indexedMap
                (\i r ->
                    if i == mi then
                        { r | status = Just UserDefined }

                    else
                        r
                )

    else
        (rankings |> List.take mi)
            ++ (rankings |> List.drop ma |> List.head |> Maybe.map (\x -> [ { x | status = Just UserDefined } ]) |> Maybe.withDefault [])
            ++ (rankings |> List.drop mi |> List.head |> Maybe.map (\x -> [ { x | status = Just UserDefined } ]) |> Maybe.withDefault [])
            ++ (rankings |> List.drop (ma + 1))


setUserRank : List GroupFixturePrediction -> String -> Int -> Int -> List GroupFixturePrediction
setUserRank groups groupName oldRank newRank =
    let
        mi =
            min oldRank newRank

        ma =
            max oldRank newRank
    in
    groups
        |> List.map
            (\g ->
                if g.groupName == groupName && mi == ma then
                    { g
                        | rankings =
                            g.rankings
                                |> List.indexedMap
                                    (\i r ->
                                        if i == mi then
                                            { r | status = Just UserDefined }

                                        else
                                            r
                                    )
                    }

                else if g.groupName == groupName then
                    { g
                        | rankings =
                            (g.rankings |> List.take mi)
                                ++ (g.rankings |> List.drop ma |> List.head |> Maybe.map (\x -> [ { x | status = Just UserDefined } ]) |> Maybe.withDefault [])
                                ++ (g.rankings |> List.drop mi |> List.head |> Maybe.map (\x -> [ { x | status = Just UserDefined } ]) |> Maybe.withDefault [])
                                ++ (g.rankings |> List.drop (ma + 1))
                    }

                else
                    g
            )


randomizeFixturePredictions : List GroupFixturePrediction -> List FixtureResult -> List GroupFixturePrediction
randomizeFixturePredictions groups results =
    let
        lookup =
            results |> List.indexedMap (\i x -> ( i, x )) |> Dict.fromList
    in
    groups
        |> List.indexedMap
            (\i group ->
                { group
                    | fixtures =
                        group.fixtures
                            |> List.indexedMap
                                (\j fixture ->
                                    { fixture | result = lookup |> Dict.get (i * 6 + j) }
                                )
                }
            )
        |> List.map
            (\group ->
                { group | rankings = mapGroupRankings group.groupName group.fixtures }
            )


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


getThirdPlaceMatchups : Session -> List String -> Cmd Msg
getThirdPlaceMatchups session groups =
    let
        config =
            Endpoint.defaultEndpointConfig
    in
    Endpoint.request
        Endpoint.thirdPlaceMatchups
        (Http.expectJson GotThirdPlaceMatchups (Json.list Json.string))
        { config
            | body = Http.jsonBody (Encode.list Encode.string groups)
            , method = "POST"
            , headers = Endpoint.useToken session
        }


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


updateFixtureResult : Int -> FixtureResult -> GroupFixturePrediction -> GroupFixturePrediction
updateFixtureResult fixtureId fixtureResult groupFixture =
    let
        fixtures =
            groupFixture.fixtures
                |> List.map
                    (\fixture ->
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
                    )
    in
    { groupFixture
        | fixtures = fixtures
        , rankings = mapGroupRankings groupFixture.groupName fixtures
    }


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
                |> Maybe.map (\comp -> comp.groups |> List.map (mapGroupFixture comp))
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
        , thirds = updateThirds fixturePredictions
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


mapGroupFixture : CompetitionInfo -> Group -> GroupFixturePrediction
mapGroupFixture competitionInfo groupDto =
    let
        mapFixture : Fixture -> Maybe FixturePrediction
        mapFixture fixtureDto =
            let
                homeTeam =
                    findTeam competitionInfo.teams fixtureDto.homeTeamId

                awayTeam =
                    findTeam competitionInfo.teams fixtureDto.awayTeamId
            in
            case ( homeTeam, awayTeam ) of
                ( Just team1, Just team2 ) ->
                    Just
                        { fixtureId = fixtureDto.id
                        , homeTeam = team1
                        , awayTeam = team2
                        , result = Nothing
                        , groupName = groupDto.name
                        , confident = False
                        }

                _ ->
                    Nothing

        fixtures =
            groupDto.fixtures |> List.filterMap mapFixture
    in
    { groupName = groupDto.name
    , fixtures = fixtures
    , rankings = mapGroupRankings groupDto.name fixtures
    }


groupBy : (a -> comparable) -> List a -> Dict comparable (List a)
groupBy getKey items =
    List.foldl
        (\item groups ->
            Dict.update (getKey item)
                (\maybeExistingItems ->
                    case maybeExistingItems of
                        Just existingItems ->
                            Just (item :: existingItems)

                        Nothing ->
                            Just [ item ]
                )
                groups
        )
        Dict.empty
        items


mapGroupRankings : String -> List FixturePrediction -> List TeamRanking
mapGroupRankings groupName fixtures =
    let
        groupTable =
            calculateGroupTable fixtures

        allResults =
            fixtures |> List.all (\x -> x.result /= Nothing)

        ptsLookup =
            groupTable |> groupBy Tuple.second |> Dict.map (\_ v -> List.length v)
    in
    groupTable
        |> List.sortBy (\( _, pts ) -> -pts)
        |> List.map
            (\( team, pts ) ->
                { status =
                    if not allResults then
                        Nothing

                    else if Dict.get pts ptsLookup == Just 1 then
                        Just Fixed

                    else
                        Just Loose
                , team = team
                , points = pts
                , groupName = groupName
                }
            )


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
        |> required "groups" (Json.list groupDecoder)
        |> required "players" (Json.list playerDecoder)


teamDecoder : Json.Decoder Team
teamDecoder =
    Json.succeed Team
        |> required "id" Json.int
        |> required "name" Json.string
        |> required "tla" Json.string


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
        |> required "fixtures" (Json.list fixtureDecoder)


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
        , ( "fixtures", Encode.list fixtureResultEncoder (model.fixturePredictions |> List.map (\x -> x.fixtures) |> List.concat) )
        , ( "qualifiers", qualifiersEncoder model )
        , ( "winner", Encode.int (model.final |> List.head |> Maybe.andThen (\x -> x.winner) |> Maybe.map .id |> Maybe.withDefault 0) )
        , ( "topScorers", Encode.list Encode.int (model.topScorers |> List.map (\x -> x.playerId)) )
        , ( "groups", Encode.list groupsEncoder model.fixturePredictions )
        , ( "thirdRankings", Encode.list Encode.int (model.thirds |> List.map (\x -> x.team.id)) )
        ]


qualifiersEncoder : Model -> Encode.Value
qualifiersEncoder model =
    Encode.object
        [ ( "roundOf32", Encode.list Encode.int (model.roundOf32 |> List.map (\m -> [ m.team1.id, m.team2.id ]) |> List.concat) )
        , ( "roundOf16", Encode.list Encode.int (model.roundOf16 |> List.map (\m -> [ m.team1.id, m.team2.id ]) |> List.concat) )
        , ( "roundOf8", Encode.list Encode.int (model.quarterFinals |> List.map (\m -> [ m.team1.id, m.team2.id ]) |> List.concat) )
        , ( "roundOf4", Encode.list Encode.int (model.semiFinals |> List.map (\m -> [ m.team1.id, m.team2.id ]) |> List.concat) )
        , ( "roundOf2", Encode.list Encode.int (model.final |> List.map (\m -> [ m.team1.id, m.team2.id ]) |> List.concat) )
        ]


groupsEncoder : GroupFixturePrediction -> Encode.Value
groupsEncoder group =
    Encode.object
        [ ( "groupName", Encode.string group.groupName )
        , ( "confidentFixture", Encode.int (group.fixtures |> List.filter (\x -> x.confident) |> List.head |> Maybe.map (\x -> x.fixtureId) |> Maybe.withDefault 0) )
        , ( "rankingOrder", Encode.list Encode.int (group.rankings |> List.map (\x -> x.team.id)) )
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
