module Main exposing (main)

import App.State exposing (init, subscriptions, update)
import App.Types exposing (Action(..), Model)
import App.View exposing (view)
import Browser


main : Program () Model Action
main =
  Browser.application
    { init = init
    , onUrlChange = UrlChanged
    , onUrlRequest = LinkClicked
    , subscriptions = subscriptions
    , update = update
    , view = view
    }
