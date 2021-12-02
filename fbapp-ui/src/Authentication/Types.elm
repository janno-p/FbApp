module Authentication.Types exposing (..)


type Action
  = ChallengeAuthentication
  | SetUser (Maybe User)
  | LogOut
  | NoOp


type AuthenticationStatus
  = Loading
  | Ready


type alias User =
  { accessToken : String
  , name : String
  }


type alias Model =
  { user : Maybe User
  , status : AuthenticationStatus
  }
