module Page exposing (Page(..), view)

import Avatar
import Browser exposing (Document)
import Html exposing (Html, a, div, footer, h5, img, nav, span, text)
import Html.Attributes exposing (alt, class)
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
        [ viewSiteToolbar page maybeUser
        , content
        , viewFooter
        ]
    }


viewFooter : Html msg
viewFooter =
    footer [] []


viewSiteToolbar : Page -> Maybe User -> Html msg
viewSiteToolbar _ maybeUser =
    let
        toolbarButtons =
            [ -- btn(flat, stretch, aria-label=Menu, click.prevent=goHome)
              a [ Route.href Route.Home ]
                [ -- div(class=row/items-center/no-wrap)
                  div [ class "flex flex-row flex-nowrap" ]
                    [ -- icon(left, name=mdi-soccer, size=lg)
                      span [ class "mdi mdi-soccer" ] []
                    , div [ class "text-left" ]
                        [ h5 [ class "py-0 my-0" ] [ text "Ennustusmäng" ]
                        , -- div(class=text-caption)
                          div [] [ text "competitionName" ]
                        ]
                    ]
                ]
            , -- space
              -- btn(icon=playlist-check, flat, stretch, title=Muudatuste logi, to=changelog)
              a [ Route.href Route.Changelog ]
                [ span [ class "mdi mdi-playlist-check" ] []
                , text "Muudatuste logi"
                ]
            ]
    in
    -- elevated
    nav []
        [ -- toolbar(class=glossy, color=primary, inverted=false)
          div [ class "glossy bg-primary flex flex-row flex-nowrap" ] ( toolbarButtons ++ (viewUser maybeUser) )
        ]


viewUser : Maybe User -> List (Html msg)
viewUser maybeUser =
    case maybeUser of
        Just user ->
            [ -- div(class=q-px-md row items-center no-wrap)
              div [ class "px-2 flex flex-row flex-nowrap items-center" ]
                [ -- icon(name=mdi-account-tie, size=1.715rem)
                  img [ alt "Avatar", Avatar.src (User.avatar user) ] []
                , -- div(class="q-pl-sm text-weight-medium")
                  div [ class "pl-1 font-medium" ] [ (User.username user |> Username.toHtml) ]
                ]
            , -- btn(icon=mdi-cog-outline, flat, stretch, title=Ava kontrollpaneel, to=dashboard)
              a [ Route.href Route.Changelog ]
                [ span [ class "mdi mdi-cog-outline" ] []
                , span [] [ text "Ava kontrollpaneel" ]
                ]
            , -- btn(icon=mdi-logout, flat, stretch, title=Logi välja, click=logout)
              a [ Route.href Route.Logout ]
                [ span [ class "mdi mdi-logout" ] []
                , span [] [ text "Logi välja" ]
                ]
            ]

        Nothing ->
            -- a(icon=google, flat, stretch, title=Logi sisse Google kontoga, href=/connect/google)
            [ a [ Route.href Route.Login ]
                [ span [ class "mdi mdi-google" ] []
                , text "Logi sisse Google kontoga"
                ]
            ]
