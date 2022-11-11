port module Main exposing (generateRandomBytes, main, randomBytes)

import Api exposing (AuthState)
import Browser exposing (Document)
import Browser.Navigation as Nav
import Html
import Http
import Json.Decode as Json
import OAuth exposing (ErrorCode(..))
import OAuth.AuthorizationCode.PKCE as OAuth
import OAuth.Refresh
import Page
import Page.Blank as Blank
import Page.Changelog as Changelog
import Page.Home as Home
import Page.LoggingOut as LoggingOut
import Page.NotFound as NotFound
import Process
import Route exposing (Route)
import Session exposing (Session, navKey)
import Task
import Url exposing (Protocol(..), Url)
import User exposing (User)



-- MODEL


type alias Configuration =
    { authorizationEndpoint : Url
    , tokenEndpoint : Url
    , userInfoEndpoint : Url
    , userInfoDecoder : Json.Decoder User
    , clientId : String
    , scope : List String
    }


type Error
    = ErrStateMismatch
    | ErrFailedToConvertBytes
    | ErrAuthorization OAuth.AuthorizationError
    | ErrAuthentication OAuth.AuthenticationError
    | ErrHTTPGetAccessToken
    | ErrHTTPGetUserInfo


type Flow
    = Idle
    | Authorized OAuth.AuthorizationCode OAuth.CodeVerifier
    | Authenticated OAuth.AuthenticationSuccess
    | Done User
    | Errored Error


type Model
    = Authentication Flow Url
    | Redirect
    | NotFound
    | Home Home.Model
    | LoggingOut
    | Changelog


configuration : Configuration
configuration =
    { authorizationEndpoint =
        { defaultHttpsUrl | host = "localhost", port_ = Just 8090, path = "/connect/authorize" }
    , tokenEndpoint =
        { defaultHttpsUrl | host = "localhost", port_ = Just 8090, path = "/connect/token" }
    , userInfoEndpoint =
        { defaultHttpsUrl | host = "localhost", port_ = Just 8090, path = "/connect/userinfo" }
    , userInfoDecoder =
        User.decoder
    , clientId =
        "fbapp-ui-client"
    , scope =
        [ "openid", "profile", "email", "roles", "offline_access" ]
    }


init : Maybe AuthState -> Url -> Nav.Key -> ( ( Model, Session ), Cmd Msg )
init maybeAuthState url navKey =
    let
        redirectUri =
            { url | query = Nothing, fragment = Nothing }

        clearUrl =
            Nav.replaceUrl navKey (Url.toString redirectUri)

        session =
            Session.fromUser navKey Nothing
    in
    case OAuth.parseCode url of
        OAuth.Empty ->
            --( Authentication Idle redirectUri session
            --, Cmd.none
            --)
            --changeRouteTo (Route.fromUrl url) (Redirect (Session.fromUser navKey Nothing))
            ( ( Authentication Idle redirectUri, session )
            , generateRandomBytes (Api.stateSize + Api.codeVerifierSize)
            )

        OAuth.Success { code, state } ->
            case maybeAuthState of
                Nothing ->
                    ( ( Authentication (Errored ErrStateMismatch) redirectUri, session )
                    , clearUrl
                    )

                Just authState ->
                    if state /= Just authState.state then
                        ( ( Authentication (Errored ErrStateMismatch) redirectUri, session )
                        , clearUrl
                        )

                    else
                        ( ( Authentication (Authorized code authState.codeVerifier) redirectUri, session )
                        , Cmd.batch
                            [ getAccessToken configuration redirectUri code authState.codeVerifier
                            , clearUrl
                            ]
                        )

        OAuth.Error error ->
            ( ( Authentication (Errored <| ErrAuthorization error) redirectUri, session )
            , clearUrl
            )


getAccessToken : Configuration -> Url -> OAuth.AuthorizationCode -> OAuth.CodeVerifier -> Cmd Msg
getAccessToken { clientId, tokenEndpoint } redirectUri code codeVerifier =
    Http.request <|
        OAuth.makeTokenRequest GotAccessToken
            { credentials =
                { clientId = clientId
                , secret = Nothing
                }
            , code = code
            , codeVerifier = codeVerifier
            , url = tokenEndpoint
            , redirectUri = redirectUri
            }


refreshAccessToken : Configuration -> OAuth.Token -> Cmd Msg
refreshAccessToken { clientId, tokenEndpoint } refreshToken =
    Http.request <|
        OAuth.Refresh.makeTokenRequest GotAccessToken
            { credentials =
                Just
                    { clientId = clientId
                    , secret = ""
                    }
            , scope = []
            , token = refreshToken
            , url = tokenEndpoint
            }


getUserInfo : Configuration -> OAuth.AuthenticationSuccess -> Cmd Msg
getUserInfo { userInfoDecoder, userInfoEndpoint } auth =
    Http.request
        { method = "GET"
        , body = Http.emptyBody
        , headers = OAuth.useToken auth.token []
        , url = Url.toString userInfoEndpoint
        , expect = Http.expectJson (GotUserInfo auth) userInfoDecoder
        , timeout = Nothing
        , tracker = Nothing
        }


gotRandomBytes : ( Model, Session ) -> List Int -> Url -> ( ( Model, Session ), Cmd Msg )
gotRandomBytes ( _, session ) bytes redirectUri =
    case Api.convertBytes bytes of
        Nothing ->
            ( ( Authentication (Errored ErrFailedToConvertBytes) redirectUri, session )
            , Cmd.none
            )

        Just { state, codeVerifier } ->
            let
                authorization =
                    { clientId = configuration.clientId
                    , redirectUri = redirectUri
                    , scope = configuration.scope
                    , state = Just state
                    , codeChallenge = OAuth.mkCodeChallenge codeVerifier
                    , url = configuration.authorizationEndpoint
                    }
            in
            ( ( Authentication Idle redirectUri, session )
            , authorization
                |> OAuth.makeAuthorizationUrl
                |> Url.toString
                |> Nav.load
            )


gotAccessToken : ( Model, Session ) -> Result Http.Error OAuth.AuthenticationSuccess -> Url -> ( ( Model, Session ), Cmd Msg )
gotAccessToken ( model, session ) authenticationResponse redirectUri =
    case authenticationResponse of
        Err (Http.BadBody body) ->
            case Json.decodeString OAuth.defaultAuthenticationErrorDecoder body of
                Ok error ->
                    ( ( Authentication (Errored <| ErrAuthentication error) redirectUri, session )
                    , Cmd.none
                    )

                _ ->
                    ( ( Authentication (Errored ErrHTTPGetAccessToken) redirectUri, session )
                    , Cmd.none
                    )

        Err _ ->
            ( ( Authentication (Errored ErrHTTPGetAccessToken) redirectUri, session )
            , Cmd.none
            )

        Ok auth ->
            ( ( Authentication (Authenticated auth) redirectUri, session ), updateSession auth )


updateSession : OAuth.AuthenticationSuccess -> Cmd Msg
updateSession auth =
    let
        refreshTokens =
            case auth.expiresIn of
                Just seconds ->
                    delay (seconds - 30) AccessTokenExpired

                Nothing ->
                    Cmd.none
    in
    Cmd.batch
        [ getUserInfo configuration auth
        , refreshTokens
        ]


delay : Int -> Msg -> Cmd Msg
delay time msg =
    Process.sleep (toFloat time * 1000)
        |> Task.andThen (always <| Task.succeed msg)
        |> Task.perform identity



-- VIEW


port generateRandomBytes : Int -> Cmd msg


port randomBytes : (List Int -> msg) -> Sub msg


view : ( Model, Session ) -> Document Msg
view ( model, session ) =
    let
        user =
            Session.user session

        viewPage page toMsg config =
            let
                { title, body } =
                    Page.view user page config
            in
            { title = title
            , body = List.map (Html.map toMsg) body
            }
    in
    case model of
        Authentication _ _ ->
            Page.view Nothing Page.Other Blank.view

        Redirect ->
            Page.view user Page.Other Blank.view

        NotFound ->
            Page.view user Page.Other NotFound.view

        Home home ->
            viewPage Page.Home GotHomeMsg (Home.view home)

        LoggingOut ->
            Page.view user Page.Other LoggingOut.view

        Changelog ->
            Page.view user Page.Other Changelog.view



-- UPDATE


type Msg
    = ChangedUrl Url
    | ClickedLink Browser.UrlRequest
    | GotHomeMsg Home.Msg
    | GotSession Session
    | GotAccessToken (Result Http.Error OAuth.AuthenticationSuccess)
    | GotUserInfo OAuth.AuthenticationSuccess (Result Http.Error User)
    | GotRandomBytes (List Int)
    | AccessTokenExpired


changeRouteTo : Maybe Route -> ( Model, Session ) -> ( ( Model, Session ), Cmd Msg )
changeRouteTo maybeRoute ( model, session ) =
    case maybeRoute of
        Nothing ->
            ( ( NotFound, session ), Cmd.none )

        Just Route.Home ->
            Home.init session
                |> updateWith Home GotHomeMsg ( model, session )

        Just Route.Login ->
            ( ( model, session ), Nav.load "/connect/google" )

        Just Route.Logout ->
            ( ( LoggingOut, session ), Nav.load "/connect/logout" )

        Just Route.Changelog ->
            ( ( Changelog, session ), Cmd.none )


update : Msg -> ( Model, Session ) -> ( ( Model, Session ), Cmd Msg )
update msg ( model, session ) =
    case ( msg, model ) of
        ( ClickedLink urlRequest, _ ) ->
            case urlRequest of
                Browser.Internal url ->
                    ( ( model, session )
                    , Nav.pushUrl (Session.navKey session) (Url.toString url)
                    )

                Browser.External href ->
                    ( ( model, session )
                    , Nav.load href
                    )

        ( ChangedUrl _, Authentication (Errored (ErrAuthorization { error })) _ ) ->
            case error of
                Custom "login_required" ->
                    changeRouteTo (Just Route.Home) ( Redirect, session )

                _ ->
                    ( ( model, session ), Cmd.none )

        ( ChangedUrl _, Authentication _ _ ) ->
            ( ( model, session ), Cmd.none )

        ( ChangedUrl url, _ ) ->
            changeRouteTo (Route.fromUrl url) ( model, session )

        ( GotHomeMsg subMsg, Home home ) ->
            Home.update subMsg home
                |> updateWith Home GotHomeMsg ( model, session )

        ( GotSession newSession, Redirect ) ->
            ( ( Redirect, newSession )
            , Route.replaceUrl (Session.navKey newSession) Route.Home
            )

        ( GotRandomBytes bytes, Authentication Idle redirectUri ) ->
            gotRandomBytes ( model, session ) bytes redirectUri

        ( GotAccessToken auth, Authentication (Authorized _ _) redirectUri ) ->
            gotAccessToken ( model, session ) auth redirectUri

        ( GotAccessToken auth, _ ) ->
            ( ( model, session )
            , case auth of
                Ok val ->
                    updateSession val

                Err _ ->
                    Cmd.none
            )

        ( GotUserInfo auth (Ok user), Authentication (Authenticated _) _ ) ->
            let
                key =
                    Session.navKey session
            in
            ( ( Redirect, Session.fromUser key (Just ( user, auth )) )
            , Route.replaceUrl key Route.Home
            )

        ( GotUserInfo auth (Ok user), _ ) ->
            ( ( model, Session.fromUser (Session.navKey session) (Just ( user, auth )) )
            , Cmd.none
            )

        ( AccessTokenExpired, _ ) ->
            case Session.refreshToken session of
                Just token ->
                    ( ( model, session ), refreshAccessToken configuration token )

                Nothing ->
                    ( ( model, session ), Cmd.none )

        ( _, _ ) ->
            ( ( model, session ), Cmd.none )


updateWith : (subModel -> Model) -> (subMsg -> Msg) -> ( Model, Session ) -> ( subModel, Cmd subMsg ) -> ( ( Model, Session ), Cmd Msg )
updateWith toModel toMsg ( _, session ) ( subModel, subCmd ) =
    ( ( toModel subModel, session )
    , Cmd.map toMsg subCmd
    )



-- SUBSCRIPTIONS


subscriptions : ( Model, Session ) -> Sub Msg
subscriptions ( model, session ) =
    case model of
        Authentication _ _ ->
            randomBytes GotRandomBytes

        NotFound ->
            Sub.none

        Redirect ->
            Session.changes GotSession (Session.navKey session)

        Home home ->
            Sub.map GotHomeMsg (Home.subscriptions home)

        LoggingOut ->
            Sub.none

        Changelog ->
            Sub.none



-- MAIN


main : Program (Maybe (List Int)) ( Model, Session ) Msg
main =
    Api.application
        { init = init
        , onUrlChange = ChangedUrl
        , onUrlRequest = ClickedLink
        , subscriptions = subscriptions
        , update = update
        , view = view
        }


defaultHttpsUrl : Url
defaultHttpsUrl =
    { protocol = Https
    , host = ""
    , path = ""
    , port_ = Nothing
    , query = Nothing
    , fragment = Nothing
    }
