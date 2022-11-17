module Page.Prediction exposing (Model, Msg, init, subscriptions, toSession, update, view)

import Html exposing (Html, div, text)
import Session exposing (Session)



-- MODEL


type alias Model =
    { session : Session
    }


init : Session -> ( Model, Cmd Msg )
init session =
    ( { session = session }
    , Cmd.none
    )



-- VIEW


view : Model -> { title : String, content : Html Msg }
view _ =
    { title = "Ennustamine"
    , content = div [] [ text "Juhhei!" ]
    }



-- UPDATE


type Msg
    = SessionUpdated Session


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case msg of
        SessionUpdated session ->
            ( { model | session = session }, Cmd.none )



-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions _ =
    Sub.none



-- EXPORT


toSession : Model -> Session
toSession model =
    model.session
