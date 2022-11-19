port module Main exposing (generateRandomBytes, main, randomBytes)

import Api exposing (AuthState)
import Api.Endpoint as Endpoint
import Browser exposing (Document)
import Browser.Navigation as Nav
import Html
import Http
import Json.Decode as Json
import Json.Decode.Pipeline exposing (required)
import OAuth exposing (ErrorCode(..))
import OAuth.AuthorizationCode.PKCE as OAuth
import Page
import Page.Blank as Blank
import Page.Changelog as Changelog
import Page.Home as Home
import Page.LoggingOut as LoggingOut
import Page.NotFound as NotFound
import Page.Prediction as Prediction
import Route exposing (Route)
import Session exposing (Session, navKey)
import Task exposing (Task)
import Time exposing (Posix)
import Url exposing (Protocol(..), Url)



-- MODEL


type alias Configuration =
    { authorizationEndpoint : Url
    , tokenEndpoint : Url
    , clientId : String
    , scope : List String
    }


type Error
    = ErrStateMismatch
    | ErrFailedToConvertBytes
    | ErrAuthorization OAuth.AuthorizationError
    | ErrAuthentication OAuth.AuthenticationError
    | ErrHttpGetAccessToken


type Flow
    = Idle
    | Authorized OAuth.AuthorizationCode OAuth.CodeVerifier
    | Authenticated OAuth.AuthenticationSuccess
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
    , competition : Maybe Competition
    , configuration : Configuration
    }


initConfiguration : Url -> Configuration
initConfiguration baseUrl =
    { authorizationEndpoint =
        { baseUrl | path = "/connect/authorize" }
    , tokenEndpoint =
        { baseUrl | path = "/connect/token" }
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
            { session = Session.init navKey
            , state = Authentication Idle redirectUri
            , competition = Nothing
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
            , getUserInfo auth
            )


getUserInfo : OAuth.AuthenticationSuccess -> Cmd Msg
getUserInfo auth =
    case ( auth.refreshToken, auth.expiresIn ) of
        ( Just refreshToken, Just expiresIn ) ->
            Task.perform (GetUserInfo auth.token refreshToken expiresIn) Time.now

        ( _, _ ) ->
            Cmd.none



-- VIEW


port generateRandomBytes : Int -> Cmd msg


port randomBytes : (List Int -> msg) -> Sub msg


view : Model -> Document Msg
view model =
    let
        user =
            Session.user model.session

        competitionName =
            model.competition |> Maybe.andThen (\x -> x.description)

        viewPage page toMsg config =
            let
                { title, body } =
                    Page.view competitionName user page config
            in
            { title = title
            , body = List.map (Html.map toMsg) body
            }
    in
    case model.state of
        Authentication _ _ ->
            Page.viewAuth

        Redirect ->
            Page.view competitionName user Page.Other Blank.view

        NotFound ->
            Page.view competitionName user Page.Other NotFound.view

        Home home ->
            viewPage Page.Home GotHomeMsg (Home.view home)

        LoggingOut ->
            Page.view competitionName user Page.Other LoggingOut.view

        Changelog ->
            Page.view competitionName user Page.Other Changelog.view

        Prediction prediction ->
            viewPage Page.Prediction GotPredictionMsg (Prediction.view prediction)



-- UPDATE


type Msg
    = ChangedUrl Url
    | ClickedLink Browser.UrlRequest
    | GotHomeMsg Home.Msg
    | GotSessionMsg Session.Msg
    | GotAccessToken (Result Http.Error OAuth.AuthenticationSuccess)
    | GotRandomBytes (List Int)
    | GotCompetitionStatus (Result Http.Error Competition)
    | GotPredictionMsg Prediction.Msg
    | GetUserInfo OAuth.Token OAuth.Token Int Posix
    | SessionLoaded Session.Msg


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
                    --changeRouteTo (Just Route.Home) { model | state = Redirect }
                    ( model, getCompetitionStatus )

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

        ( GotRandomBytes bytes, Authentication Idle redirectUri ) ->
            gotRandomBytes model bytes redirectUri

        ( GotRandomBytes _, _ ) ->
            ( model, Cmd.none )

        ( GotAccessToken auth, Authentication (Authorized _ _) redirectUri ) ->
            gotAccessToken model auth redirectUri

        ( GotAccessToken auth, _ ) ->
            ( model
            , auth |> Result.map getUserInfo |> Result.withDefault Cmd.none
            )

        ( GotCompetitionStatus (Ok competition), _ ) ->
            ( { model | state = Redirect, competition = Just competition }
            , routeToDefault competition model.session
            )

        ( GotCompetitionStatus _, _ ) ->
            ( model, Route.replaceUrl (Session.navKey model.session) Route.Home )

        ( GotPredictionMsg subMsg, Prediction prediction ) ->
            Prediction.update subMsg prediction
                |> updateWith Prediction GotPredictionMsg model

        ( GotPredictionMsg _, _ ) ->
            ( model, Cmd.none )

        ( GotSessionMsg subMsg, _ ) ->
            let
                ( session, subCmd ) =
                    Session.update ( model.configuration.clientId, model.configuration.tokenEndpoint ) subMsg model.session
            in
            ( { model | session = session }, Cmd.map GotSessionMsg subCmd )

        ( GetUserInfo accessToken refreshToken expiresIn updatedAt, _ ) ->
            let
                subMsg =
                    Session.createTicket accessToken refreshToken (Time.millisToPosix (Time.posixToMillis updatedAt + (1000 * expiresIn)))
                        |> Session.getUserInfo
            in
            ( model, Cmd.map SessionLoaded subMsg )

        ( SessionLoaded subMsg, _ ) ->
            let
                ( session, subCmd ) =
                    Session.update ( model.configuration.clientId, model.configuration.tokenEndpoint ) subMsg model.session
            in
            ( { model | session = session }
            , Cmd.batch
                [ Cmd.map GotSessionMsg subCmd
                , getCompetitionStatus
                ]
            )


updateWith : (subModel -> State) -> (subMsg -> Msg) -> Model -> ( subModel, Cmd subMsg ) -> ( Model, Cmd Msg )
updateWith toState toMsg model ( subModel, subCmd ) =
    ( { model | state = toState subModel }
    , Cmd.map toMsg subCmd
    )


routeToDefault : Competition -> Session -> Cmd Msg
routeToDefault competition session =
    case ( Session.user session, competition.status ) of
        ( Just _, AcceptPredictions ) ->
            Route.replaceUrl (Session.navKey session) Route.Prediction

        ( _, _ ) ->
            Route.replaceUrl (Session.navKey session) Route.Home



-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions model =
    let
        sub =
            case model.state of
                Authentication _ _ ->
                    randomBytes GotRandomBytes

                NotFound ->
                    Sub.none

                Redirect ->
                    Sub.none

                Home home ->
                    Sub.map GotHomeMsg (Home.subscriptions home)

                LoggingOut ->
                    Sub.none

                Changelog ->
                    Sub.none

                Prediction prediction ->
                    Sub.map GotPredictionMsg (Prediction.subscriptions prediction)
    in
    Sub.batch
        [ sub
        , Sub.map GotSessionMsg (Session.subscriptions model.session)
        ]



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


getCompetitionStatus : Cmd Msg
getCompetitionStatus =
    Endpoint.request Endpoint.competitionStatus (Http.expectJson GotCompetitionStatus competitionDecoder) Endpoint.defaultEndpointConfig


type CompetitionStatus
    = AcceptPredictions
    | InProgress
    | NotActive


type alias Competition =
    { startDate : Maybe Posix
    , description : Maybe String
    , status : CompetitionStatus
    }


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
