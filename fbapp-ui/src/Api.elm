port module Api exposing (Cred, application, logout, userChanges, username)

import Browser
import Browser.Navigation as Nav
import Json.Decode as Decode exposing (Decoder, Value)
import Json.Decode.Pipeline exposing (required, requiredAt)
import Url exposing (Url)
import Username exposing (Username)


-- CRED


type Cred
  = Cred Username String


username : Cred -> Username
username (Cred val _) =
    val


credDecoder : Decoder Cred
credDecoder =
  Decode.succeed Cred
    |> requiredAt ["profile", "name"] Username.decoder
    |> required "access_token" Decode.string


-- PERSISTENCE


port onStoreChange : (Value -> msg) -> Sub msg


userChanges : (Maybe user -> msg) -> Decoder (Cred -> user) -> Sub msg
userChanges toMsg decoder =
  onStoreChange (\value -> toMsg (decodeFromChange decoder value))


decodeFromChange : Decoder (Cred -> user) -> Value -> Maybe user
decodeFromChange userDecoder val =
  Decode.decodeValue (decoderFromCred userDecoder) val
    |> Result.toMaybe

port signOut : Maybe Value -> Cmd msg

logout : Cmd msg
logout =
  signOut Nothing


-- SERIALIZATION
-- APPLICATION


application :
  Decoder (Cred -> user)
  ->
    { init : Maybe user -> Url -> Nav.Key -> ( model, Cmd msg )
    , onUrlChange : Url -> msg
    , onUrlRequest : Browser.UrlRequest -> msg
    , subscriptions : model -> Sub msg
    , update : msg -> model -> ( model, Cmd msg )
    , view : model -> Browser.Document msg
    }
  -> Program Value model msg
application userDecoder config =
  let
    init flags url navKey =
      config.init (decodeFromChange userDecoder flags) url navKey
  in
  Browser.application
    { init = init
    , onUrlChange = config.onUrlChange
    , onUrlRequest = config.onUrlRequest
    , subscriptions = config.subscriptions
    , update = config.update
    , view = config.view
    }


-- HTTP


decoderFromCred : Decoder (Cred -> a) -> Decoder a
decoderFromCred decoder =
  Decode.map2 (\fromCred cred -> fromCred cred)
    decoder
    credDecoder
