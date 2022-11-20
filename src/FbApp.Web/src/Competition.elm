module Competition exposing (Competition, CompetitionStatus(..), default, request)

import Api.Endpoint as Endpoint
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (required)
import Time exposing (Posix)


type alias Competition =
    { startDate : Maybe Posix
    , description : Maybe String
    , status : CompetitionStatus
    }


type CompetitionStatus
    = AcceptPredictions
    | InProgress
    | NotActive


default : Competition
default =
    { startDate = Nothing
    , description = Nothing
    , status = NotActive
    }


request : (Result Http.Error Competition -> msg) -> Cmd msg
request toMsg =
    Endpoint.request
        Endpoint.competitionStatus
        (Http.expectJson toMsg competitionDecoder)
        Endpoint.defaultEndpointConfig


competitionDecoder : Json.Decoder Competition
competitionDecoder =
    Json.succeed Competition
        |> required "startDate" (Json.nullable Json.int |> Json.map (Maybe.map Time.millisToPosix))
        |> required "description" (Json.nullable Json.string)
        |> required "status" competitionStatusDecoder


competitionStatusDecoder : Json.Decoder CompetitionStatus
competitionStatusDecoder =
    Json.string
        |> Json.map
            (\val ->
                case val of
                    "accept-predictions" ->
                        AcceptPredictions

                    "competition-running" ->
                        InProgress

                    _ ->
                        NotActive
            )
