module Api.Endpoint exposing
    ( Endpoint
    , competitionInfo
    , competitionStatus
    , defaultEndpointConfig
    , defaultFixture
    , fixture
    , leaderboard
    , prediction
    , request
    , savePrediction
    , useToken
    )

import Http
import OAuth
import Session exposing (Session)
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


competitionInfo : Endpoint
competitionInfo =
    url [ "predict", "fixtures" ] []


savePrediction : Endpoint
savePrediction =
    url [ "predict" ] []


prediction : Endpoint
prediction =
    url [ "prediction" ] []


defaultFixture : Endpoint
defaultFixture =
    url [ "fixtures" ] []


fixture : String -> Endpoint
fixture fixtureId =
    url [ "fixtures", fixtureId ] []


leaderboard : Endpoint
leaderboard =
    url [ "prediction", "board" ] []


useToken : Session -> List Http.Header
useToken session =
    case Session.accessToken session of
        Just token ->
            OAuth.useToken token []

        Nothing ->
            []
