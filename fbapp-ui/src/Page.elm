module Page exposing (Page(..), view)

import Avatar
import Browser exposing (Document)
import Html exposing (Html, a, footer, img, nav, text)
import Html.Attributes exposing (alt)
import User exposing (User)
import Username
import Route


-- MODEL


type Page
  = Other
  | Home


-- VIEW

view : Maybe User -> Page -> { title : String, content : Html msg } -> Document msg
view maybeUser page { title, content } =
  { title = title ++ " - FbApp"
  , body =
    [ viewHeader page maybeUser
    , content
    , viewFooter
    ]
  }


viewHeader : Page -> Maybe User -> Html msg
viewHeader _ maybeUser =
  case maybeUser of
    Just user ->
      nav []
        [ img [ alt "Avatar", Avatar.src (User.avatar user)] []
        , User.username user |> Username.toHtml
        , a [ Route.href Route.Logout ] [ text "Log out" ]
        ]

    Nothing ->
      nav [] [
        a [ Route.href Route.Login ] [ text "Log in with Google account" ]
      ]


viewFooter : Html msg
viewFooter =
  footer [] []
