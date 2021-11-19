module HelloWorld exposing (helloWorld)

import Html exposing (Html, div, h1, p, a, button, code, text)
import Html.Attributes exposing (href)
import Html.Events exposing (onClick)
import Msg exposing (Msg(..))
import Html.Events exposing (onDoubleClick)


helloWorld : Int -> Html Msg
helloWorld model =
    div []
        [ h1 [] [ text "Hello, Vite + Elm!?" ]
        , p []
            [ a [ href "https://vitejs.dev/guide/features.html" ] [ text "Vite Documentation" ]
            , text " | "
            , a [ href "https://guide.elm-lang.org/" ] [ text "Elm Documentation" ]
            ]
        , button [ onClick Increment, onDoubleClick Decrement ] [ text ("count is: " ++ String.fromInt model) ]
        , p []
            [ text "Edit "
            , code [] [ text "src/Main.elm" ]
            , text " to test auto refresh"
            ]
        ]
