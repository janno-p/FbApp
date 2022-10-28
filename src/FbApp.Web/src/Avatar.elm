module Avatar exposing (Avatar, decoder, src)

import Asset
import Json.Decode as Decode exposing (Decoder)
import Html exposing (Attribute)
import Html.Attributes


-- TYPES


type Avatar
    = Avatar (Maybe String)


-- CREATE


decoder : Decoder Avatar
decoder =
    Decode.map Avatar (Decode.nullable Decode.string)


-- TRANSFORM


src : Avatar -> Attribute msg
src (Avatar maybeUrl) =
    case maybeUrl of
        Nothing ->
            Asset.src Asset.defaultAvatar

        Just "" ->
            Asset.src Asset.defaultAvatar

        Just url ->
            Html.Attributes.src url
