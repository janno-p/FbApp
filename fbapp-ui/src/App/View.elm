module App.View exposing (..)

import App.Types exposing (Action(..), Model)
import Authentication.Types exposing (AuthenticationStatus(..))
import Authentication.View as Auth
import Browser
import Debug exposing (toString)
import Html exposing (Html, a, b, button, code, div, h1, img, li, p, text, ul)
import Html.Attributes exposing (href, src, style)
import Html.Events exposing (onClick)
import Url


helloWorld : Int -> Html Action
helloWorld model =
  div []
        [ h1 [] [ text "Hello, Vite + Elm!?" ]
        , p []
            [ a [ href "https://vitejs.dev/guide/features.html" ] [ text "Vite Documentation" ]
            , text " | "
            , a [ href "https://guide.elm-lang.org/" ] [ text "Elm Documentation" ]
            ]
        , button [ onClick Increment ] [ text ("count is: " ++ String.fromInt model) ]
        , p []
            [ text "Edit "
            , code [] [ text "src/Main.elm" ]
            , text " to test auto refresh"
            ]
        ]


view : Model -> Browser.Document Action
view model =
  { title = "FbApp"
  , body =
      case model.authentication.status of
        Loading ->
          [ text "⏳⏳⏳ Authentication in progress. Please wait ..."]
        Ready ->
          [ text "The current URL is: "
          , b [] [ text (Url.toString model.url) ]
          , Auth.loginButton model.authentication |> Html.map AuthenticationAction
          , ul []
              [ viewLink "/"
              , viewLink "/home"
              , viewLink "/profile"
              , viewLink "/reviews/the-century-of-the-self"
              , viewLink "/reviews/public-opinion"
              , viewLink "/reviews/shah-of-shahs"
              ]
          , div []
              [ p [] [ text (toString model.authentication.user) ]
              , img [ src "/logo.png", style "width" "300px" ] []
              , helloWorld model.value
              ]
          ]
  }


viewLink : String -> Html msg
viewLink path =
  li [] [ a [ href path ] [ text path ] ]
