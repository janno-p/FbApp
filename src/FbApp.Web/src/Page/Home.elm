module Page.Home exposing (Model, Msg, init, subscriptions, update, view)

import Browser.Navigation as Nav
import Competition exposing (Competition, CompetitionStatus(..))
import Html exposing (Html, a, button, div, p, span, text)
import Html.Attributes exposing (class)
import Html.Events exposing (onClick)
import Route
import Session exposing (Session)
import String exposing (padLeft)
import Task
import Time exposing (Posix, every)
import Url.Builder exposing (absolute, string)



-- MODEL


type alias Model =
    { competition : Competition
    , currentTime : Maybe Posix
    }


init : Session -> Competition -> ( Model, Cmd Msg )
init session competition =
    let
        cmd =
            case competition.status of
                AcceptPredictions ->
                    Task.perform UpdateCounter Time.now

                InProgress ->
                    Route.replaceUrl (Session.navKey session) (Route.Fixture Nothing)

                NotActive ->
                    Cmd.none
    in
    ( { competition = competition
      , currentTime = Nothing
      }
    , cmd
    )



-- VIEW


view : Session -> Model -> { title : String, content : Html Msg }
view session model =
    { title = "Home"
    , content = viewContent session model
    }


viewContent : Session -> Model -> Html Msg
viewContent session model =
    div []
        (case model.competition.status of
            Competition.AcceptPredictions ->
                viewAcceptPredictions session model

            Competition.InProgress ->
                [ p [] [ text "Vaata tulemusi!" ] ]

            Competition.NotActive ->
                [ p [] [ text "Hetkel ei ole midagi toimumas!" ] ]
        )


minutes : Int
minutes =
    60 * 1000


hours : Int
hours =
    60 * 60 * 1000


days : Int
days =
    24 * 60 * 60 * 1000


viewAcceptPredictions : Session -> Model -> List (Html Msg)
viewAcceptPredictions session model =
    let
        millis =
            model.currentTime
                |> Maybe.andThen (\ct -> model.competition.startDate |> Maybe.map (\st -> Time.posixToMillis st - Time.posixToMillis ct))

        remainingTime =
            millis
                |> Maybe.map
                    (\x ->
                        { days = x // days
                        , hours = modBy days x // hours
                        , minutes = modBy hours (modBy days x) // minutes
                        }
                    )
    in
    case remainingTime of
        Just time ->
            viewCountdown time :: viewChallenge session

        Nothing ->
            []


viewChallenge : Session -> List (Html Msg)
viewChallenge session =
    case Session.user session of
        Just _ ->
            [ div [ class "flex flex-row justify-center pt-6" ]
                [ a
                    [ class "rounded-md px-4 py-2 border border-sky-600 bg-sky-200 hover:bg-sky-400 drop-shadow-md cursor-pointer"
                    , Route.href Route.Prediction
                    ]
                    [ div [ class "items-center flex flex-row gap-1" ]
                        [ text "Lisa oma ennustus"
                        , span [ class "mdi mdi-chevron-double-right" ] []
                        ]
                    ]
                ]
            ]

        Nothing ->
            [ div [ class "text-center pt-12" ]
                [ span [ class "mr-[0.5ch]" ] [ text "Ennustuse tegemiseks logi sisse kasutades oma" ]
                , div [ class "inline-flex flex-nowrap" ]
                    [ span [ class "mdi mdi-google" ] []
                    , text "oogle kontot"
                    ]
                ]
            , div [ class "flex flex-row justify-center pt-6" ]
                [ button
                    [ class "rounded-md px-4 py-2 border border-sky-600 bg-sky-200 hover:bg-sky-400 drop-shadow-md cursor-pointer"
                    , onClick LoginToPrediction
                    ]
                    [ div [ class "items-center flex flex-row gap-1" ]
                        [ span [ class "mdi mdi-login" ] []
                        , text "Logi sisse"
                        ]
                    ]
                ]
            ]


viewCountdown : { days : Int, hours : Int, minutes : Int } -> Html Msg
viewCountdown remainingTime =
    let
        viewUnit unit value =
            div [ class "flex flex-col gap-2" ]
                [ div [ class "text-center font-sans text-6xl" ] [ text (String.fromInt value |> padLeft 2 '0') ]
                , div [ class "text-center uppercase text-sm" ] [ text unit ]
                ]
    in
    div []
        [ div [ class "text-center text-2xl mt-8" ] [ text "Turniiri alguseni on jäänud" ]
        , div [ class "flex flex-row justify-center gap-6" ]
            [ viewUnit "päeva" remainingTime.days
            , viewUnit "tundi" remainingTime.hours
            , viewUnit "minutit" remainingTime.minutes
            ]
        ]



-- UPDATE


type Msg
    = UpdateCounter Posix
    | LoginToPrediction


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        UpdateCounter currentTime ->
            ( { model | currentTime = Just currentTime }
            , Cmd.none
            )

        LoginToPrediction ->
            ( model, absolute [ "connect", "google" ] [ string "returnUrl" (Route.toString Route.Prediction) ] |> Nav.load )



-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions model =
    case model.competition.status of
        AcceptPredictions ->
            every 60000 UpdateCounter

        InProgress ->
            Sub.none

        NotActive ->
            Sub.none
