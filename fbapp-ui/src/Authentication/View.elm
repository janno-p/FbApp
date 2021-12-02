module Authentication.View exposing (..)


import Authentication.Types exposing (Action(..), Model)
import Html exposing (Html, button, text)
import Html.Events exposing (onClick)


viewLoginButton : Model -> Html Action
viewLoginButton model =
  case model.user of
    Just user ->
      button [ onClick LogOut ] [ text ("Log out (" ++ user.name ++ ")") ]

    Nothing ->
      button [ onClick ChallengeAuthentication ] [ text "Login with your Google account" ]
