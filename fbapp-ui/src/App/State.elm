module App.State exposing (..)

import App.Types exposing (..)
import Authentication.State as Auth
import Browser
import Browser.Navigation as Nav
import Url


init : () -> Url.Url -> Nav.Key -> ( Model, Cmd Action )
init _ url key =
  let
    ( authentication, value ) =
      ( Auth.init, 0 )
  in
  (Model authentication value key url, Cmd.none )


update : Action -> Model -> ( Model, Cmd Action )
update action model =
  case action of
    LinkClicked urlRequest ->
      case urlRequest of
        Browser.Internal url ->
          ( model, Nav.pushUrl model.key (Url.toString url) )

        Browser.External href ->
          ( model, Nav.load href )

    UrlChanged url ->
      ( { model | url = url }
      , Cmd.none
      )

    Increment ->
      ( { model | value = model.value + 1 }
      , Cmd.none
      )

    Decrement ->
      ( { model | value = model.value - 1 }
      , Cmd.none
      )

    AuthenticationAction authAction ->
      let
        ( authentication, cmd ) =
          Auth.update authAction model.authentication
      in
      ( { model | authentication = authentication }
      , Cmd.map AuthenticationAction cmd
      )


subscriptions : Model -> Sub Action
subscriptions model =
  Sub.batch
    [ Sub.map AuthenticationAction (Auth.subscriptions model.authentication)
    ]
