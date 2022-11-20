module Api exposing (AuthState, application, codeVerifierSize, convertBytes, stateSize)

import Base64.Encode as Base64
import Browser
import Browser.Navigation as Nav
import Bytes exposing (Bytes)
import Bytes.Encode as Bytes
import Json.Decode as Decode exposing (Decoder)
import OAuth
import OAuth.AuthorizationCode.PKCE as OAuth
import Url exposing (Url)


type alias AuthState =
    { state : String
    , codeVerifier : OAuth.CodeVerifier
    , path : String
    }



-- PERSISTENCE
-- SERIALIZATION
-- APPLICATION


application :
    { init : Maybe AuthState -> Url -> Nav.Key -> ( model, Cmd msg )
    , onUrlChange : Url -> msg
    , onUrlRequest : Browser.UrlRequest -> msg
    , subscriptions : model -> Sub msg
    , update : msg -> model -> ( model, Cmd msg )
    , view : model -> Browser.Document msg
    }
    -> Program (Maybe ( List Int, String )) model msg
application config =
    --let
    --    init flags url navKey =
    --        config.init flags url navKey
    --in
    Browser.application
        { init = Maybe.andThen convertBytes >> config.init
        , onUrlChange = config.onUrlChange
        , onUrlRequest = config.onUrlRequest
        , subscriptions = config.subscriptions
        , update = config.update
        , view = config.view
        }



-- HTTP


stateSize : Int
stateSize =
    8


codeVerifierSize : Int
codeVerifierSize =
    32


toBytes : List Int -> Bytes
toBytes =
    List.map Bytes.unsignedInt8 >> Bytes.sequence >> Bytes.encode


base64 : Bytes -> String
base64 =
    Base64.bytes >> Base64.encode


convertBytes : ( List Int, String ) -> Maybe AuthState
convertBytes ( bytes, path ) =
    if List.length bytes < (stateSize + codeVerifierSize) then
        Nothing

    else
        let
            state =
                bytes
                    |> List.take stateSize
                    |> toBytes
                    |> base64

            maybeCodeVerifier =
                bytes
                    |> List.drop stateSize
                    |> toBytes
                    |> OAuth.codeVerifierFromBytes
        in
        Maybe.map (\codeVerifier -> { state = state, codeVerifier = codeVerifier, path = path }) maybeCodeVerifier
