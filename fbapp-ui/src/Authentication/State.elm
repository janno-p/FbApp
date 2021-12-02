port module Authentication.State exposing (..)


import Authentication.Rest exposing (..)
import Authentication.Types exposing (..)
import Browser.Navigation as Nav
import Json.Decode


port authenticated : (Json.Decode.Value -> msg) -> Sub msg


init : Model
init =
  { user = Nothing
  , status = Loading
  }


update : Action -> Model -> ( Model, Cmd Action )
update action model =
  case action of
    ChallengeAuthentication ->
      ( model
      , Nav.load "/connect/google"
      )

    SetUser user ->
      ( { model | user = user, status = Ready }
      , Cmd.none
      )

    LogOut ->
      ( model
      , Nav.load "/connect/logout"
      )

    NoOp ->
      ( model
      , Cmd.none
      )


subscriptions : Model -> Sub Action
subscriptions _ =
  authenticated mapAuthenticated
