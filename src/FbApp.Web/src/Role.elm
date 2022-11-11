module Role exposing (Role(..), RoleList, decoder)

import Array
import Json.Decode as Decode exposing (Decoder)


type alias RoleList =
    List Role


type Role
    = Admin


decoder : Decoder RoleList
decoder =
    Decode.array roleValueDecoder
        |> Decode.andThen
            (\arr ->
                arr
                    |> Array.toList
                    |> List.filterMap identity
                    |> Decode.succeed
            )


roleValueDecoder : Decoder (Maybe Role)
roleValueDecoder =
    Decode.string
        |> Decode.andThen
            (\str ->
                case str of
                    "admin" ->
                        Decode.succeed (Just Admin)

                    _ ->
                        Decode.succeed Nothing
            )
