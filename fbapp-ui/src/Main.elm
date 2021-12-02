module Main exposing (main, update)

import Authentication.State
import Authentication.Types
import Authentication.View
import Browser
import Browser.Navigation as Nav
import Debug exposing (toString)
import HelloWorld exposing (helloWorld)
import Html exposing (..)
import Html.Attributes exposing (..)
import Html.Events exposing (..)
import Json.Decode exposing (..)
import Msg exposing (Msg(..))
import Url


main : Program () Model Msg
main =
  Browser.application
    { init = init
    , onUrlChange = UrlChanged
    , onUrlRequest = LinkClicked
    , subscriptions = subscriptions
    , update = update
    , view = view
    }


type alias Model =
  { authentication : Authentication.Types.Model
  , value : Int
  , key : Nav.Key
  , url : Url.Url
  }


init : () -> Url.Url -> Nav.Key -> ( Model, Cmd Msg )
init flags url key =
  let
    ( authentication, value ) =
      ( Authentication.State.init, 0 )
  in
  (Model authentication value key url, Cmd.none )


update : Msg -> Model -> ( Model, Cmd Msg )
update msg model =
  case msg of
    LinkClicked urlRequest ->
      case urlRequest of
        Browser.Internal url ->
          ( model, Nav.pushUrl model.key (Url.toString url) )

        Browser.External href ->
          ( model, Nav.load href )

    UrlChanged url ->
      ( { model | url = url }
      , Cmd.none
      )

    Increment ->
      ( { model | value = model.value + 1 }
      , Cmd.none
      )

    Decrement ->
      ( { model | value = model.value - 1 }
      , Cmd.none
      )

    AuthenticationMsg action ->
      let
        ( authentication, cmd ) =
          Authentication.State.update action model.authentication
      in
      ( { model | authentication = authentication }
      , Cmd.map AuthenticationMsg cmd
      )


subscriptions : Model -> Sub Msg
subscriptions model =
  Sub.batch
    [ Sub.map AuthenticationMsg (Authentication.State.subscriptions model.authentication)
    ]


view : Model -> Browser.Document Msg
view model =
  { title = "FbApp"
  , body =
      [ text "The current URL is: "
      , b [] [ text (Url.toString model.url) ]
      , Authentication.View.viewLoginButton model.authentication |> Html.map AuthenticationMsg
      , ul []
          [ viewLink "/"
          , viewLink "/home"
          , viewLink "/profile"
          , viewLink "/reviews/the-century-of-the-self"
          , viewLink "/reviews/public-opinion"
          , viewLink "/reviews/shah-of-shahs"
          ]
      , div []
          [ p [] [ text (toString model.authentication.user) ]
          , img [ src "/logo.png", style "width" "300px" ] []
          , helloWorld model.value
          ]
      ]
  }


viewLink : String -> Html msg
viewLink path =
  li [] [ a [ href path ] [ text path ] ]
