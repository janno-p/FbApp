module User exposing (User, avatar, decoder, isAdmin, username)

import Api exposing (Cred)
import Avatar exposing (Avatar)
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline exposing (custom)
import Role exposing (Role(..), RoleList)
import Username exposing (Username)



-- TYPES


type User
    = User Avatar RoleList Cred



-- INFO


username : User -> Username
username (User _ _ val) =
    Api.username val


avatar : User -> Avatar
avatar (User val _ _) =
    val


isAdmin : User -> Bool
isAdmin (User _ roles _) =
    roles |> List.any ((==) Admin)



-- SERIALIZATION


decoder : Decoder (Cred -> User)
decoder =
    Decode.succeed User
        |> custom (Decode.at [ "profile", "picture" ] Avatar.decoder)
        |> custom roleDecoder


roleDecoder : Decoder RoleList
roleDecoder =
    Decode.maybe (Decode.at [ "profile", "role" ] Role.decoder)
        |> Decode.map
            (\maybeRoles ->
                case maybeRoles of
                    Just roles ->
                        roles

                    Nothing ->
                        []
            )
