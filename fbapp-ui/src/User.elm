module User exposing (User, avatar, decoder, username)

import Api exposing (Cred)
import Avatar exposing (Avatar)
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline exposing (custom)
import Username exposing (Username)


-- TYPES


type User
    = User Avatar Cred


-- INFO


username : User -> Username
username (User _ val) =
    Api.username val


avatar : User -> Avatar
avatar (User val _) =
    val


-- SERIALIZATION


decoder : Decoder (Cred -> User)
decoder =
    Decode.succeed User
        |> custom (Decode.at [ "profile", "picture" ] Avatar.decoder)
