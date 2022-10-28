module Username exposing (Username, decoder, toHtml)

import Html exposing (Html)
import Json.Decode as Decode exposing (Decoder)


-- TYPES


type Username
    = Username String


-- CREATE


decoder : Decoder Username
decoder =
    Decode.map Username Decode.string


-- TRANSFORM


toHtml : Username -> Html msg
toHtml (Username username) =
    Html.text username
