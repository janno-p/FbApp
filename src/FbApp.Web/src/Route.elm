module Route exposing (Route(..), fromUrl, href, replaceUrl, toString)

import Browser.Navigation as Nav
import Html exposing (Attribute)
import Html.Attributes as Attr
import Url exposing (Url)
import Url.Builder exposing (absolute)
import Url.Parser as Parser exposing ((</>), Parser, oneOf, s, string)



-- TYPES


type Route
    = Home
    | Login
    | Logout
    | Changelog
    | Prediction
    | Fixture (Maybe String)
    | Leaderboard



-- ROUTING


parser : Parser (Route -> a) a
parser =
    oneOf
        [ Parser.map Home Parser.top
        , Parser.map Login (s "login")
        , Parser.map Logout (s "logout")
        , Parser.map Changelog (s "changelog")
        , Parser.map Prediction (s "prediction")
        , Parser.map (Just >> Fixture) (s "fixture" </> string)
        , Parser.map (Fixture Nothing) (s "fixture")
        , Parser.map Leaderboard (s "leaderboard")
        ]



-- PUBLIC HELPERS


href : Route -> Attribute msg
href targetRoute =
    Attr.href (toString targetRoute)


fromUrl : Url -> Maybe Route
fromUrl url =
    Parser.parse parser url


replaceUrl : Nav.Key -> Route -> Cmd msg
replaceUrl key route =
    Nav.replaceUrl key (toString route)



-- INTERNAL


toString : Route -> String
toString route =
    absolute (toPieces route) []


toPieces : Route -> List String
toPieces page =
    case page of
        Home ->
            []

        Login ->
            [ "login" ]

        Logout ->
            [ "logout" ]

        Changelog ->
            [ "changelog" ]

        Prediction ->
            [ "prediction" ]

        Fixture (Just fixtureId) ->
            [ "fixture", fixtureId ]

        Fixture Nothing ->
            [ "fixture" ]

        Leaderboard ->
            [ "leaderboard" ]
