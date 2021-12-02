module Authentication.Types exposing (..)


type Action
  = ChallengeAuthentication
  | SetUser (Maybe User)
  | LogOut
  | NoOp


type alias User =
  { accessToken : String
  , name : String
  }


type alias Model =
  { user : Maybe User
  }
