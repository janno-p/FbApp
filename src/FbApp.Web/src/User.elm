module User exposing (User, avatar, decoder, isAdmin, username)

import Avatar exposing (Avatar)
import Json.Decode as Decode exposing (Decoder)
import Json.Decode.Pipeline exposing (custom, required)
import Role exposing (Role(..), RoleList)
import Username exposing (Username)



-- TYPES


type User
    = User Avatar RoleList Username



-- INFO


username : User -> Username
username (User _ _ val) =
    val


avatar : User -> Avatar
avatar (User val _ _) =
    val


isAdmin : User -> Bool
isAdmin (User _ roles _) =
    roles |> List.any ((==) Admin)



-- SERIALIZATION


decoder : Decoder User
decoder =
    Decode.succeed User
        |> custom (Decode.field "picture" Avatar.decoder)
        |> custom roleDecoder
        |> required "name" Username.decoder


roleDecoder : Decoder RoleList
roleDecoder =
    Decode.maybe (Decode.field "role" Role.decoder)
        |> Decode.map
            (\maybeRoles ->
                case maybeRoles of
                    Just roles ->
                        roles

                    Nothing ->
                        []
            )
