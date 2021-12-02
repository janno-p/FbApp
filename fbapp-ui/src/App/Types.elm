module App.Types exposing (..)

import Browser
import Browser.Navigation as Nav
import Url
import Authentication.Types as Auth


type Action
  = LinkClicked Browser.UrlRequest
  | UrlChanged Url.Url
  | Increment
  | Decrement
  | AuthenticationAction Auth.Action


type alias Model =
  { authentication : Auth.Model
  , value : Int
  , key : Nav.Key
  , url : Url.Url
  }
