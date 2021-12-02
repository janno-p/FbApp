module Msg exposing (Msg(..))

import Browser
import Json.Decode exposing (..)
import Url
import Authentication.Types


type Msg
  = LinkClicked Browser.UrlRequest
  | UrlChanged Url.Url
  | Increment
  | Decrement
  | AuthenticationMsg Authentication.Types.Action
