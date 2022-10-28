module Session exposing (Session, changes, fromUser, navKey, user)

import Api
import Browser.Navigation as Nav
import User exposing (User)


-- TYPES


type Session
    = Authenticated Nav.Key User
    | Guest Nav.Key


-- INFO


user : Session -> Maybe User
user session =
    case session of
        Authenticated _ val ->
            Just val

        Guest _ ->
            Nothing


navKey : Session -> Nav.Key
navKey session =
    case session of
        Authenticated key _ ->
            key

        Guest key ->
            key


-- CHANGES


changes : (Session -> msg) -> Nav.Key -> Sub msg
changes toMsg key =
    Api.userChanges (\maybeUser -> toMsg (fromUser key maybeUser)) User.decoder


fromUser : Nav.Key -> Maybe User -> Session
fromUser key maybeUser =
    case maybeUser of
        Just userVal ->
            Authenticated key userVal

        Nothing ->
            Guest key
