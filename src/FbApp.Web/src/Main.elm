port module Main exposing (generateRandomBytes, main, randomBytes)

import Api exposing (AuthState)
import Api.Endpoint as Endpoint exposing (competitionStatus)
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
import Page.Prediction as Prediction
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
    | ErrHttpGetAccessToken
    | ErrHttpGetUserInfo


type Flow
    = Idle
    | Authorized OAuth.AuthorizationCode OAuth.CodeVerifier
    | Authenticated OAuth.AuthenticationSuccess
    | Done User
    | Errored Error


type State
    = Authentication Flow Url
    | Redirect
    | NotFound
    | Home Home.Model
    | LoggingOut
    | Changelog
    | Prediction Prediction.Model


type alias Model =
    { state : State
    , session : Session
    , competitionStatus : Maybe CompetitionStatus
    , configuration : Configuration
    }


initConfiguration : Url -> Configuration
initConfiguration baseUrl =
    { authorizationEndpoint =
        { baseUrl | path = "/connect/authorize" }
    , tokenEndpoint =
        { baseUrl | path = "/connect/token" }
    , userInfoEndpoint =
        { baseUrl | path = "/connect/userinfo" }
    , userInfoDecoder =
        User.decoder
    , clientId =
        "fbapp-ui-client"
    , scope =
        [ "openid", "profile", "email", "roles", "offline_access" ]
    }


init : Maybe AuthState -> Url -> Nav.Key -> ( Model, Cmd Msg )
init maybeAuthState url navKey =
    let
        redirectUri =
            { url | query = Nothing, fragment = Nothing, path = "/" }

        clearUrl =
            Nav.replaceUrl navKey (Url.toString redirectUri)

        model =
            { session = Session.fromUser navKey Nothing
            , state = Authentication Idle redirectUri
            , competitionStatus = Nothing
            , configuration = initConfiguration { url | query = Nothing, fragment = Nothing, path = "" }
            }
    in
    case OAuth.parseCode url of
        OAuth.Empty ->
            ( model
            , generateRandomBytes (Api.stateSize + Api.codeVerifierSize)
            )

        OAuth.Success { code, state } ->
            case maybeAuthState of
                Nothing ->
                    ( { model | state = Authentication (Errored ErrStateMismatch) redirectUri }
                    , clearUrl
                    )

                Just authState ->
                    if state /= Just authState.state then
                        ( { model | state = Authentication (Errored ErrStateMismatch) redirectUri }
                        , clearUrl
                        )

                    else
                        ( { model | state = Authentication (Authorized code authState.codeVerifier) redirectUri }
                        , Cmd.batch
                            [ getAccessToken model.configuration redirectUri code authState.codeVerifier
                            , clearUrl
                            ]
                        )

        OAuth.Error error ->
            ( { model | state = Authentication (Errored <| ErrAuthorization error) redirectUri }
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


gotRandomBytes : Model -> List Int -> Url -> ( Model, Cmd Msg )
gotRandomBytes model bytes redirectUri =
    case Api.convertBytes bytes of
        Nothing ->
            ( { model | state = Authentication (Errored ErrFailedToConvertBytes) redirectUri }
            , Cmd.none
            )

        Just { state, codeVerifier } ->
            let
                authorization =
                    { clientId = model.configuration.clientId
                    , redirectUri = redirectUri
                    , scope = model.configuration.scope
                    , state = Just state
                    , codeChallenge = OAuth.mkCodeChallenge codeVerifier
                    , url = model.configuration.authorizationEndpoint
                    }
            in
            ( { model | state = Authentication Idle redirectUri }
            , authorization
                |> OAuth.makeAuthorizationUrl
                |> Url.toString
                |> Nav.load
            )


gotAccessToken : Model -> Result Http.Error OAuth.AuthenticationSuccess -> Url -> ( Model, Cmd Msg )
gotAccessToken model authenticationResponse redirectUri =
    case authenticationResponse of
        Err (Http.BadBody body) ->
            case Json.decodeString OAuth.defaultAuthenticationErrorDecoder body of
                Ok error ->
                    ( { model | state = Authentication (Errored <| ErrAuthentication error) redirectUri }
                    , Cmd.none
                    )

                _ ->
                    ( { model | state = Authentication (Errored ErrHttpGetAccessToken) redirectUri }
                    , Cmd.none
                    )

        Err _ ->
            ( { model | state = Authentication (Errored ErrHttpGetAccessToken) redirectUri }
            , Cmd.none
            )

        Ok auth ->
            ( { model | state = Authentication (Authenticated auth) redirectUri }
            , updateSession model.configuration auth
            )


updateSession : Configuration -> OAuth.AuthenticationSuccess -> Cmd Msg
updateSession configuration auth =
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


view : Model -> Document Msg
view model =
    let
        user =
            Session.user model.session

        viewPage page toMsg config =
            let
                { title, body } =
                    Page.view user page config
            in
            { title = title
            , body = List.map (Html.map toMsg) body
            }
    in
    case model.state of
        Authentication _ _ ->
            Page.viewAuth

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

        Prediction prediction ->
            viewPage Page.Prediction GotPredictionMsg (Prediction.view prediction)



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
    | GotCompetitionStatus (Result Http.Error CompetitionStatus)
    | GotPredictionMsg Prediction.Msg


changeRouteTo : Maybe Route -> Model -> ( Model, Cmd Msg )
changeRouteTo maybeRoute model =
    case maybeRoute of
        Nothing ->
            ( { model | state = NotFound }
            , Cmd.none
            )

        Just Route.Home ->
            Home.init model.session
                |> updateWith Home GotHomeMsg model

        Just Route.Login ->
            ( model, Nav.load "/connect/google" )

        Just Route.Logout ->
            ( { model | state = LoggingOut }, Nav.load "/connect/logout" )

        Just Route.Changelog ->
            ( { model | state = Changelog }, Cmd.none )

        Just Route.Prediction ->
            Prediction.init model.session
                |> updateWith Prediction GotPredictionMsg model


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
    case ( msg, model.state ) of
        ( ClickedLink urlRequest, _ ) ->
            case urlRequest of
                Browser.Internal url ->
                    ( model
                    , Nav.pushUrl (Session.navKey model.session) (Url.toString url)
                    )

                Browser.External href ->
                    ( model
                    , Nav.load href
                    )

        ( ChangedUrl _, Authentication (Errored (ErrAuthorization { error })) _ ) ->
            case error of
                Custom "login_required" ->
                    changeRouteTo (Just Route.Home) { model | state = Redirect }

                _ ->
                    ( model, Cmd.none )

        ( ChangedUrl _, Authentication _ _ ) ->
            ( model, Cmd.none )

        ( ChangedUrl url, _ ) ->
            changeRouteTo (Route.fromUrl url) model

        ( GotHomeMsg subMsg, Home home ) ->
            Home.update subMsg home
                |> updateWith Home GotHomeMsg model

        ( GotHomeMsg _, _ ) ->
            ( model, Cmd.none )

        ( GotSession newSession, Redirect ) ->
            ( { model | state = Redirect, session = newSession }
            , Route.replaceUrl (Session.navKey newSession) Route.Home
            )

        ( GotSession _, _ ) ->
            ( model, Cmd.none )

        ( GotRandomBytes bytes, Authentication Idle redirectUri ) ->
            gotRandomBytes model bytes redirectUri

        ( GotRandomBytes _, _ ) ->
            ( model, Cmd.none )

        ( GotAccessToken auth, Authentication (Authorized _ _) redirectUri ) ->
            gotAccessToken model auth redirectUri

        ( GotAccessToken auth, _ ) ->
            ( model
            , case auth of
                Ok val ->
                    updateSession model.configuration val

                Err _ ->
                    Cmd.none
            )

        ( GotUserInfo auth (Ok user), Authentication (Authenticated _) _ ) ->
            let
                key =
                    Session.navKey model.session
            in
            ( { model | state = Redirect, session = Session.fromUser key (Just ( user, auth )) }
            , getCompetitionStatus
            )

        ( GotUserInfo auth (Ok user), _ ) ->
            ( { model | session = Session.fromUser (Session.navKey model.session) (Just ( user, auth )) }
            , Cmd.none
            )

        ( GotUserInfo _ _, _ ) ->
            ( model, Cmd.none )

        ( AccessTokenExpired, _ ) ->
            case Session.refreshToken model.session of
                Just token ->
                    ( model, refreshAccessToken model.configuration token )

                Nothing ->
                    ( model, Cmd.none )

        ( GotCompetitionStatus (Ok competitionStatus), _ ) ->
            ( { model | competitionStatus = Just competitionStatus }
            , routeToDefault competitionStatus model.session
            )

        ( GotCompetitionStatus _, _ ) ->
            ( model, Route.replaceUrl (Session.navKey model.session) Route.Home )

        ( GotPredictionMsg subMsg, Prediction prediction ) ->
            Prediction.update subMsg prediction
                |> updateWith Prediction GotPredictionMsg model

        ( GotPredictionMsg _, _ ) ->
            ( model, Cmd.none )


updateWith : (subModel -> State) -> (subMsg -> Msg) -> Model -> ( subModel, Cmd subMsg ) -> ( Model, Cmd Msg )
updateWith toState toMsg model ( subModel, subCmd ) =
    ( { model | state = toState subModel }
    , Cmd.map toMsg subCmd
    )


routeToDefault : CompetitionStatus -> Session -> Cmd Msg
routeToDefault competitionStatus session =
    case competitionStatus of
        InProgress ->
            Route.replaceUrl (Session.navKey session) Route.Home

        AcceptPredictions ->
            Route.replaceUrl (Session.navKey session) Route.Prediction

        NotActive ->
            Route.replaceUrl (Session.navKey session) Route.Home



-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions model =
    case model.state of
        Authentication _ _ ->
            randomBytes GotRandomBytes

        NotFound ->
            Sub.none

        Redirect ->
            Session.changes GotSession (Session.navKey model.session)

        Home home ->
            Sub.map GotHomeMsg (Home.subscriptions home)

        LoggingOut ->
            Sub.none

        Changelog ->
            Sub.none

        Prediction prediction ->
            Sub.map GotPredictionMsg (Prediction.subscriptions prediction)



-- MAIN


main : Program (Maybe (List Int)) Model Msg
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


getCompetitionStatus : Cmd Msg
getCompetitionStatus =
    Endpoint.request Endpoint.competitionStatus (Http.expectJson GotCompetitionStatus competitionStatusDecoder) Endpoint.defaultEndpointConfig


type CompetitionStatus
    = AcceptPredictions
    | InProgress
    | NotActive


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
