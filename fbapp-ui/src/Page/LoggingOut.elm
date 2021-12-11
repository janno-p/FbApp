module Page.LoggingOut exposing (view)

import Html exposing (Html, div, text)


view : { title : String, content : Html msg }
view =
    { title = "Logging out"
    , content = div [] [ text "Logging out" ]
    }
