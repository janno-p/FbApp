module Authentication.Rest exposing (..)


import Authentication.Types exposing (Action(..), User)
import Debug exposing (log)
import Json.Decode exposing (Decoder, field, map2, maybe, string)


decodeUser : Decoder (Maybe User)
decodeUser =
  maybe
    (map2 User
      (field "access_token" string)
      (field "profile" (field "name" string)))


mapAuthenticated : Json.Decode.Value -> Action
mapAuthenticated modelJson =
  case (Json.Decode.decodeValue decodeUser modelJson) of
    Ok user ->
      SetUser user

    Err errorMessage ->
      let
        _ =
          log "Error in mapAuthenticated:" errorMessage
      in
        NoOp
