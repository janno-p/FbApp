module Api.Endpoint exposing (Endpoint, competitionStatus, defaultEndpointConfig, fixturePredictions, predictions, request)

import Http
import Url.Builder exposing (QueryParameter)


request : Endpoint -> Http.Expect msg -> EndpointConfig -> Cmd msg
request endpoint expect config =
    Http.request
        { body = config.body
        , expect = expect
        , headers = config.headers
        , method = config.method
        , timeout = config.timeout
        , url = unwrap endpoint
        , tracker = config.tracker
        }


type alias EndpointConfig =
    { body : Http.Body
    , headers : List Http.Header
    , method : String
    , timeout : Maybe Float
    , tracker : Maybe String
    }


defaultEndpointConfig : EndpointConfig
defaultEndpointConfig =
    { body = Http.emptyBody
    , headers = []
    , method = "GET"
    , timeout = Nothing
    , tracker = Nothing
    }



-- TYPES


type Endpoint
    = Endpoint String


unwrap : Endpoint -> String
unwrap (Endpoint str) =
    str


url : List String -> List QueryParameter -> Endpoint
url paths queryParams =
    Endpoint (Url.Builder.absolute ("api" :: paths) queryParams)



-- COMPETITION ENDPOINTS


competitionStatus : Endpoint
competitionStatus =
    url [ "competition", "status" ] []


fixturePredictions : Endpoint
fixturePredictions =
    url [ "predict", "fixtures" ] []


predictions : Endpoint
predictions =
    url [ "predict", "current" ] []
