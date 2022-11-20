module Page exposing (Page(..), view, viewAuth)

import Avatar
import Browser exposing (Document)
import Html exposing (Html, a, div, footer, h5, img, nav, span, text)
import Html.Attributes exposing (alt, class, title)
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
    nav [ class "glossy bg-sky-600 flex flex-row flex-nowrap items-center text-white px-4 py-1.5 gap-2" ]
        [ a [ Route.href Route.Home, class "text-white grow-0" ]
            [ span [ class "mdi mdi-soccer text-3xl" ] [] ]
        , a [ Route.href Route.Home, class "flex flex-col grow text-white gap-2" ]
            [ span [ class "grow-0 text-xl leading-4" ]
                [ text "Ennustusmäng" ]
            , div [ class "grow uppercase text-sm leading-4" ]
                [ text (competitionName |> Maybe.withDefault "") ]
            ]
        , div [ class "grow-0 py-0.5" ] (viewUser maybeUser)
        ]


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
            [ a [ Route.href Route.Login, class "text-white", title "Logi sisse Google kontoga" ]
                [ div [ class "border border-white rounded-full w-8 h-8 flex items-center justify-center" ] [ span [ class "mdi mdi-google" ] [] ] ]
            ]
