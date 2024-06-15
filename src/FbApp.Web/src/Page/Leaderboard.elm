module Page.Leaderboard exposing (Model, Msg, init, update, view)

import Api.Endpoint as Endpoint exposing (defaultEndpointConfig)
import Html exposing (Html, div, span, sup, text)
import Html.Attributes exposing (class, title)
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (required)
import Page exposing (PageTab(..), viewResultsTabs)
import Round
import Session exposing (Session)


type alias Model =
    { predictionResults : Maybe (List PredictionResult)
    }


type alias PredictionResult =
    { name : String
    , matches : Int
    , qualifiers : Int
    , quarterFinals : Int
    , semiFinals : Int
    , finals : Int
    , winner : Int
    , topScorerGoals : Int
    , topScorerHit : Int
    , total : Int
    , ratio : Float
    , rank : Int
    , scorerFixed : Bool
    }


init : Session -> ( Model, Cmd Msg )
init session =
    let
        model =
            { predictionResults = Nothing
            }
    in
    ( model, loadLeaderboard session )


view : Model -> { title : String, content : Html Msg }
view model =
    let
        content =
            div []
                [ viewResultsTabs LeaderboardTab
                , case model.predictionResults of
                    Just predictionResults ->
                        viewLeaderboardTable predictionResults

                    Nothing ->
                        div [ class "mt-8 text-center" ] [ text "Tuleb varsti ..." ]
                ]
    in
    { title = "Ennustuste punktitabel"
    , content = content
    }


viewLeaderboardTable : List PredictionResult -> Html Msg
viewLeaderboardTable predictionResults =
    let
        minRatio =
            predictionResults
                |> List.map (\x -> x.ratio)
                |> List.minimum
                |> Maybe.withDefault 0.0

        maxRatio =
            predictionResults
                |> List.map (\x -> x.ratio)
                |> List.maximum
                |> Maybe.withDefault 0.0

        ratioRange =
            ( minRatio, maxRatio )
    in
    div [ class "sm:rounded-md sm:border border-gray-200 sm:w-[40rem] sm:mx-auto mt-2 sm:mt-8 sm:shadow-lg py-4" ]
        (div [ class "hidden sm:grid grid-cols-[2rem_1fr_3.5rem] sm:grid-cols-[2rem_1fr_repeat(8,2rem)_2.5rem_1rem] font-semibold px-8 border-b border-gray-200 pb-2 gap-2" ]
            [ div [] []
            , div [] []
            , div [ title "Alagrupimängude tulemused", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-format-list-group" ] [] ]
            , div [ title "Edasipääsejad", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-music-note-sixteenth" ] [] ]
            , div [ title "Veerandfinalistid", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-music-note-eighth" ] [] ]
            , div [ title "Poolfinalistid", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-music-note-quarter" ] [] ]
            , div [ title "Finalistid", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-music-note-half" ] [] ]
            , div [ title "Võitja", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-music-note-whole" ] [] ]
            , div [ title "Väravaküttide poolt löödud väravad", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-shoe-cleat" ] [] ]
            , div [ title "Väravakütt", class "text-center hidden sm:block" ] [ span [ class "mdi mdi-medal" ] [] ]
            , div [ title "Punkte kokku", class "text-center" ] [ span [ class "mdi mdi-sigma" ] [] ]
            , div [ title "Trend", class "text-center" ] [ span [ class "mdi mdi-trending-up" ] [] ]
            ]
            :: List.map (viewPredictionResult ratioRange) predictionResults
        )


viewPredictionResult : ( Float, Float ) -> PredictionResult -> Html Msg
viewPredictionResult ratioRange predictionResult =
    div [ class "grid grid-cols-[2rem_1fr_3.5rem] sm:grid-cols-[2rem_1fr_repeat(8,2rem)_2.5rem_1rem] px-8 leading-8 border-b last:border-b-0 sm:last:border-b border-gray-200 gap-2 la" ]
        [ div [ class "text-center pr-2" ]
            [ if predictionResult.rank == 1 then
                text "🥇"

              else if predictionResult.rank == 2 then
                text "🥈"

              else if predictionResult.rank == 3 then
                text "🥉"

              else
                text (String.fromInt predictionResult.rank ++ ".")
            ]
        , div [ class "capitalize" ] [ text predictionResult.name ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.matches) ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.qualifiers) ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.quarterFinals) ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.semiFinals) ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.finals) ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.winner) ]
        , div [ class "text-center hidden sm:block" ] [ text (String.fromInt predictionResult.topScorerGoals) ]
        , div [ class "text-center hidden sm:block" ]
            (if predictionResult.scorerFixed || predictionResult.topScorerHit == 0 then
                [ text (String.fromInt predictionResult.topScorerHit) ]

             else
                [ span [ class "text-stone-500 text-xs" ]
                    [ text "+"
                    , text (String.fromInt predictionResult.topScorerHit)
                    ]
                ]
            )
        , div [ class "tabular-nums text-center border-l border-gray-200 space-x-1" ]
            [ span []
                (if predictionResult.topScorerHit /= 0 && predictionResult.scorerFixed == False then
                    [ text (String.fromInt predictionResult.total)
                    , text "*"
                    ]

                 else
                    [ text (String.fromInt predictionResult.total) ]
                )
            ]
        , div [ class "text-left" ] [ viewRatio predictionResult.ratio ratioRange ]
        ]


viewRatio : Float -> ( Float, Float ) -> Html Msg
viewRatio value ( minRatio, maxRatio ) =
    let
        step =
            (maxRatio - minRatio) / 5.0

        ( ratioColor, ratioIcon ) =
            if value < minRatio + step then
                ( "text-negative", "mdi-chevron-double-down" )

            else if value < minRatio + 2.0 * step then
                ( "text-warning", "mdi-chevron-down" )

            else if value < minRatio + 3.0 * step then
                ( "text-info", "mdi-equal" )

            else if value < minRatio + 4.0 * step then
                ( "text-lime-400", "mdi-chevron-up" )

            else
                ( "text-positive", "mdi-chevron-double-up" )
    in
    span [ class "mdi", class ratioColor, class ratioIcon, title (Round.round 3 value ++ " %") ] []


type Msg
    = LeaderboardLoaded (Result Http.Error (List PredictionResult))


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        LeaderboardLoaded (Ok predictionResults) ->
            ( { model | predictionResults = Just predictionResults }, Cmd.none )

        LeaderboardLoaded _ ->
            ( model, Cmd.none )


loadLeaderboard : Session -> Cmd Msg
loadLeaderboard session =
    Endpoint.request
        Endpoint.leaderboard
        (Http.expectJson LeaderboardLoaded leaderboardDecoder)
        { defaultEndpointConfig | headers = Endpoint.useToken session }


leaderboardDecoder : Json.Decoder (List PredictionResult)
leaderboardDecoder =
    Json.list predictionResultDecoder


mapPredictionResult : String -> List Int -> Bool -> Int -> Float -> Int -> PredictionResult
mapPredictionResult name points scorerFixed total ratio rank =
    { name = name
    , matches =
        case points of
            x :: _ ->
                x

            _ ->
                0
    , qualifiers =
        case points of
            _ :: x :: _ ->
                x

            _ ->
                0
    , quarterFinals =
        case points of
            _ :: _ :: x :: _ ->
                x

            _ ->
                0
    , semiFinals =
        case points of
            _ :: _ :: _ :: x :: _ ->
                x

            _ ->
                0
    , finals =
        case points of
            _ :: _ :: _ :: _ :: x :: _ ->
                x

            _ ->
                0
    , winner =
        case points of
            _ :: _ :: _ :: _ :: _ :: x :: _ ->
                x

            _ ->
                0
    , topScorerHit =
        case points of
            _ :: _ :: _ :: _ :: _ :: _ :: x :: _ ->
                x

            _ ->
                0
    , topScorerGoals =
        case points of
            _ :: _ :: _ :: _ :: _ :: _ :: _ :: y :: _ ->
                y

            _ ->
                0
    , total = total
    , ratio = ratio
    , rank = rank
    , scorerFixed = scorerFixed
    }


predictionResultDecoder : Json.Decoder PredictionResult
predictionResultDecoder =
    Json.succeed mapPredictionResult
        |> required "name" Json.string
        |> required "points" (Json.list Json.int)
        |> required "scorerFixed" Json.bool
        |> required "total" Json.int
        |> required "ratio" Json.float
        |> required "rank" Json.int
