module Page.Leaderboard exposing (Model, Msg, init, view)

import Html exposing (Html, div, text)
import Html.Attributes exposing (class)
import Page exposing (PageTab(..), viewResultsTabs)
import Session exposing (Session)


type alias Model =
    ()


init : Session -> ( Model, Cmd Msg )
init _ =
    ( (), Cmd.none )


view : Model -> { title : String, content : Html Msg }
view _ =
    let
        content =
            div []
                [ viewResultsTabs LeaderboardTab
                , div [ class "mt-8 text-center" ] [ text "Tuleb varsti ..." ]
                ]
    in
    { title = "Ennustuste punktitable"
    , content = content
    }


type alias Msg =
    ()
