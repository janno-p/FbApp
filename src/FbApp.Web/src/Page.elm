module Page exposing (Page(..), view, viewAuth)

import Avatar
import Browser exposing (Document)
import Html exposing (Html, a, div, footer, h5, img, nav, span, text)
import Html.Attributes exposing (alt, class)
import Route
import User exposing (User)
import Username



-- MODEL


type Page
    = Other
    | Home
    | Prediction



-- VIEW


view : Maybe String -> Maybe User -> Page -> { title : String, content : Html msg } -> Document msg
view competitionName maybeUser page { title, content } =
    { title = title ++ " - FbApp"
    , body =
        [ viewSiteToolbar page competitionName maybeUser
        , content
        , viewFooter
        ]
    }


viewAuth : Document msg
viewAuth =
    { title = "FbApp"
    , body =
        [ text "⏳⏳⏳ Loading application. Please wait ..." ]
    }


viewFooter : Html msg
viewFooter =
    footer [] []


viewSiteToolbar : Page -> Maybe String -> Maybe User -> Html msg
viewSiteToolbar _ competitionName maybeUser =
    let
        toolbarButtons =
            [ a
                [ Route.href Route.Home
                , class "text-white"
                ]
                [ div [ class "flex flex-row flex-nowrap" ]
                    [ span [ class "mdi mdi-soccer" ] []
                    , div [ class "text-left" ]
                        [ h5 [ class "py-0 my-0" ] [ text "Ennustusmäng" ]
                        , div [] [ text (competitionName |> Maybe.withDefault "") ]
                        ]
                    ]
                ]
            , a
                [ Route.href Route.Changelog
                , class "text-white"
                ]
                [ span [ class "mdi mdi-playlist-check" ] []
                , text "Muudatuste logi"
                ]
            ]
    in
    nav []
        [ div [ class "glossy bg-primary flex flex-row flex-nowrap text-white" ] (toolbarButtons ++ viewUser maybeUser) ]


viewUser : Maybe User -> List (Html msg)
viewUser maybeUser =
    case maybeUser of
        Just user ->
            let
                userInfo =
                    [ div [ class "px-2 flex flex-row flex-nowrap items-center" ]
                        [ img [ alt "Avatar", class "w-10 h-10 rounded-full", Avatar.src (User.avatar user) ] []
                        , div [ class "pl-1 font-medium" ] [ User.username user |> Username.toHtml ]
                        ]
                    ]

                controlPanel =
                    if User.isAdmin user then
                        [ a
                            [ Route.href Route.Changelog
                            , class "text-white"
                            ]
                            [ span [ class "mdi mdi-cog-outline" ] []
                            , span [] [ text "Ava kontrollpaneel" ]
                            ]
                        ]

                    else
                        []

                logout =
                    [ a
                        [ Route.href Route.Logout
                        , class "text-white"
                        ]
                        [ span [ class "mdi mdi-logout" ] []
                        , span [] [ text "Logi välja" ]
                        ]
                    ]
            in
            userInfo ++ controlPanel ++ logout

        Nothing ->
            [ a
                [ Route.href Route.Login
                , class "text-white"
                ]
                [ span [ class "mdi mdi-google" ] []
                , text "Logi sisse Google kontoga"
                ]
            ]
