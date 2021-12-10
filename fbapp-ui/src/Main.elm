module Main exposing (main)

import Api
import Browser exposing (Document)
import Browser.Navigation as Nav
import Html
import Json.Decode exposing (Value)
import Page
import Page.Blank as Blank
import Page.Home as Home
import Page.LoggingOut as LoggingOut
import Page.NotFound as NotFound
import Route exposing (Route)
import Session exposing (Session)
import User exposing (User)
import Url exposing (Url)


-- MODEL


type Model
  = Redirect Session
  | NotFound Session
  | Home Home.Model
  | LoggingOut Session


init : Maybe User -> Url -> Nav.Key -> ( Model, Cmd Msg )
init maybeUser url navKey =
  changeRouteTo (Route.fromUrl url)
    (Redirect (Session.fromUser navKey maybeUser))


-- VIEW


view : Model -> Document Msg
view model =
  let
    user =
      Session.user (toSession model)

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
    Redirect _ ->
      Page.view user Page.Other Blank.view

    NotFound _ ->
      Page.view user Page.Other NotFound.view

    Home home ->
      viewPage Page.Home GotHomeMsg (Home.view home)

    LoggingOut _ ->
      Page.view user Page.Other LoggingOut.view


-- UPDATE


type Msg
  = ChangedUrl Url
  | ClickedLink Browser.UrlRequest
  | GotHomeMsg Home.Msg
  | GotSession Session


toSession : Model -> Session
toSession page =
  case page of
    Redirect session ->
      session

    NotFound session ->
      session

    Home home ->
      Home.toSession home

    LoggingOut session ->
      session


changeRouteTo : Maybe Route -> Model -> ( Model, Cmd Msg )
changeRouteTo maybeRoute model =
  let
    session =
      toSession model
  in
  case maybeRoute of
    Nothing ->
      ( NotFound session, Cmd.none )

    Just Route.Home ->
      Home.init session
        |> updateWith Home GotHomeMsg model

    Just Route.Login ->
      ( model, Nav.load "/connect/google" )

    Just Route.Logout ->
      ( LoggingOut session, Api.logout )


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
  case ( msg, model ) of
    ( ClickedLink urlRequest, _ ) ->
      case urlRequest of
        Browser.Internal url ->
          ( model
          , Nav.pushUrl (Session.navKey (toSession model)) (Url.toString url)
          )

        Browser.External href ->
          ( model
          , Nav.load href
          )

    ( ChangedUrl url, _ ) ->
      changeRouteTo (Route.fromUrl url) model

    ( GotHomeMsg subMsg, Home home ) ->
      Home.update subMsg home
        |> updateWith Home GotHomeMsg model

    ( GotSession session, Redirect _ ) ->
      ( Redirect session
      , Route.replaceUrl (Session.navKey session) Route.Home
      )

    ( _, _ ) ->
      ( model, Cmd.none )


updateWith : (subModel -> Model) -> (subMsg -> Msg) -> Model -> ( subModel, Cmd subMsg ) -> ( Model, Cmd Msg )
updateWith toModel toMsg _ ( subModel, subCmd ) =
  ( toModel subModel
  , Cmd.map toMsg subCmd
  )


-- SUBSCRIPTIONS


subscriptions : Model -> Sub Msg
subscriptions model =
  case model of
    NotFound _ ->
      Sub.none

    Redirect _ ->
      Session.changes GotSession (Session.navKey (toSession model))

    Home home ->
      Sub.map GotHomeMsg (Home.subscriptions home)

    LoggingOut _ ->
      Sub.none


-- MAIN


main : Program Value Model Msg
main =
  Api.application User.decoder
    { init = init
    , onUrlChange = ChangedUrl
    , onUrlRequest = ClickedLink
    , subscriptions = subscriptions
    , update = update
    , view = view
    }
