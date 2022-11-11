module Session exposing (Session, changes, fromUser, navKey, refreshToken, user)

import Browser.Navigation as Nav
import OAuth exposing (Token)
import OAuth.AuthorizationCode.PKCE as OAuth
import User exposing (User)



-- TYPES


type Session
    = Authenticated Nav.Key User OAuth.AuthenticationSuccess
    | Guest Nav.Key



-- INFO


user : Session -> Maybe User
user session =
    case session of
        Authenticated _ val _ ->
            Just val

        Guest _ ->
            Nothing


navKey : Session -> Nav.Key
navKey session =
    case session of
        Authenticated key _ _ ->
            key

        Guest key ->
            key


refreshToken : Session -> Maybe Token
refreshToken session =
    case session of
        Authenticated _ _ auth ->
            auth.refreshToken

        Guest _ ->
            Nothing



-- CHANGES


changes : (Session -> msg) -> Nav.Key -> Sub msg
changes _ _ =
    --Api.userChanges (\maybeUser -> toMsg (fromUser key maybeUser)) User.decoder
    Sub.none


fromUser : Nav.Key -> Maybe ( User, OAuth.AuthenticationSuccess ) -> Session
fromUser key maybeUser =
    case maybeUser of
        Just ( userVal, auth ) ->
            Authenticated key userVal auth

        Nothing ->
            Guest key
