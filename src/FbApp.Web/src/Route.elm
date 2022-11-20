module Route exposing (Route(..), fromUrl, href, replaceUrl, toString)

import Browser.Navigation as Nav
import Html exposing (Attribute)
import Html.Attributes as Attr
import Url exposing (Url)
import Url.Parser as Parser exposing (Parser, oneOf, s)



-- TYPES


type Route
    = Home
    | Login
    | Logout
    | Changelog
    | Prediction



-- ROUTING


parser : Parser (Route -> a) a
parser =
    oneOf
        [ Parser.map Home Parser.top
        , Parser.map Login (s "login")
        , Parser.map Logout (s "logout")
        , Parser.map Changelog (s "changelog")
        , Parser.map Prediction (s "prediction")
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
    "/" ++ String.join "/" (toPieces route)


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
