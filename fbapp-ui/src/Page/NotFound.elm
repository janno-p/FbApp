module Page.NotFound exposing (view)

import Html exposing (Html, text)


-- VIEW


view : { title : String, content : Html msg }
view =
  { title = "Page Not Found"
  , content = text "Not Found"
  }
