module Session exposing (Msg, Session, accessToken, createTicket, getUserInfo, init, navKey, refreshToken, subscriptions, update, user)

import Browser.Navigation as Nav
import Http
import OAuth
import OAuth.AuthorizationCode.PKCE as OAuth
import OAuth.Refresh
import Time exposing (Posix, every)
import Url exposing (Url)
import Url.Builder exposing (absolute)
import User exposing (User)



-- TYPES


type alias Ticket =
    { accessToken : OAuth.Token
    , refreshToken : OAuth.Token
    , expiresAt : Posix
    }


type Session
    = Authenticated Nav.Key User Ticket
    | Guest Nav.Key


init : Nav.Key -> Session
init key =
    Guest key



-- INFO


user : Session -> Maybe User
user session =
    case session of
        Authenticated _ val _ ->
            Just val

        Guest _ ->
            Nothing


navKey : Session -> Nav.Key
navKey session =
    case session of
        Authenticated key _ _ ->
            key

        Guest key ->
            key


accessToken : Session -> Maybe OAuth.Token
accessToken session =
    case session of
        Authenticated _ _ ticket ->
            Just ticket.accessToken

        Guest _ ->
            Nothing


refreshToken : Session -> Maybe OAuth.Token
refreshToken session =
    case session of
        Authenticated _ _ ticket ->
            Just ticket.refreshToken

        Guest _ ->
            Nothing


createTicket : OAuth.Token -> OAuth.Token -> Posix -> Ticket
createTicket accessTokenVal refreshTokenVal expiresAt =
    { accessToken = accessTokenVal
    , refreshToken = refreshTokenVal
    , expiresAt = expiresAt
    }



-- UPDATE


type Msg
    = UpdateToken Ticket Posix
    | GotAccessToken Posix (Result Http.Error OAuth.AuthenticationSuccess)
    | GotUserInfo Ticket (Result Http.Error User)


update : ( String, Url ) -> Msg -> Session -> ( Session, Cmd Msg )
update configuration msg session =
    case msg of
        UpdateToken ticket currentTime ->
            if Time.posixToMillis ticket.expiresAt - Time.posixToMillis currentTime < 70000 then
                ( session, refreshAccessToken configuration ticket.refreshToken currentTime )

            else
                ( session, Cmd.none )

        GotAccessToken updatedAt (Ok auth) ->
            case ( session, auth.refreshToken, auth.expiresIn ) of
                ( Authenticated navKeyVal userVal _, Just token, Just expiresIn ) ->
                    let
                        ticket =
                            createTicket auth.token token (Time.millisToPosix (Time.posixToMillis updatedAt + (1000 * expiresIn)))
                    in
                    ( Authenticated navKeyVal userVal ticket
                    , getUserInfo ticket
                    )

                ( _, _, _ ) ->
                    ( session, Cmd.none )

        GotAccessToken _ _ ->
            ( session, Cmd.none )

        GotUserInfo ticket (Ok userInfo) ->
            ( Authenticated (navKey session) userInfo ticket, Cmd.none )

        GotUserInfo _ _ ->
            ( session, Cmd.none )


refreshAccessToken : ( String, Url ) -> OAuth.Token -> Posix -> Cmd Msg
refreshAccessToken ( clientId, tokenEndpoint ) token currentTime =
    Http.request <|
        OAuth.Refresh.makeTokenRequest (GotAccessToken currentTime)
            { credentials =
                Just
                    { clientId = clientId
                    , secret = ""
                    }
            , scope = []
            , token = token
            , url = tokenEndpoint
            }


getUserInfo : Ticket -> Cmd Msg
getUserInfo ticket =
    Http.request
        { method = "GET"
        , body = Http.emptyBody
        , headers = OAuth.useToken ticket.accessToken []
        , url = absolute [ "connect", "userinfo" ] []
        , expect = Http.expectJson (GotUserInfo ticket) User.decoder
        , timeout = Nothing
        , tracker = Nothing
        }



-- SUBSCRIPTIONS


subscriptions : Session -> Sub Msg
subscriptions session =
    case session of
        Authenticated _ _ ticket ->
            every 15000 (UpdateToken ticket)

        Guest _ ->
            Sub.none
