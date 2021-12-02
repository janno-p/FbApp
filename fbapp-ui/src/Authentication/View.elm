module Authentication.View exposing (..)


import Authentication.Types exposing (Action(..), Model)
import Html exposing (Html, button, text)
import Html.Attributes exposing (class)
import Html.Events exposing (onClick)


loginButton : Model -> Html Action
loginButton model =
  case model.user of
    Just user ->
      button ( onClick LogOut :: buttonClasses ) [ text ("Log out (" ++ user.name ++ ")") ]

    Nothing ->
      button ( onClick ChallengeAuthentication :: buttonClasses ) [ text "Login with your Google account" ]


buttonClasses : List (Html.Attribute Action)
buttonClasses =
  [ "block"
  , "px-4"
  , "py-2"
  , "text-white"
  , "transition"
  , "duration-100"
  , "ease-in-out"
  , "bg-blue-500"
  , "border"
  , "border-transparent"
  , "rounded"
  , "shadow-sm"
  , "hover:bg-blue-600"
  , "focus:border-blue-500"
  , "focus:ring-2"
  , "focus:ring-blue-500"
  , "focus:outline-none"
  , "focus:ring-opacity-50"
  , "disabled:opacity-50"
  , "cursor-pointer"
  ] |> List.map class
