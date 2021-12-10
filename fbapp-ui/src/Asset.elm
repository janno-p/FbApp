module Asset exposing (defaultAvatar, src)

import Html exposing (Attribute)
import Html.Attributes as Attr


-- TYPES


type Image
  = Image String


-- IMAGES


defaultAvatar : Image
defaultAvatar =
  image "smiley-cyrus.jpg"


image : String -> Image
image filename =
  Image ("/assets/images/" ++ filename)


-- USING IMAGES


src : Image -> Attribute msg
src (Image url) =
  Attr.src url
