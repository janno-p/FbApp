module Page.NotFound exposing (view)

import Html exposing (Html, a, div, text)
import Html.Attributes exposing (class, style)
import Route



-- VIEW


view : { title : String, content : Html msg }
view =
    { title = "Page Not Found"
    , content =
        -- class="fullscreen bg-blue text-white text-center q-pa-md flex flex-center"
        div [ class "w-full bg-blue-400 text-white text-center p-4 flex items-center justify-center" ]
            [ div []
                [ -- style="font-size: 30vh"
                  div [ style "font-size" "30vh" ] [ text "404" ]

                -- class="text-h2" style="opacity:.4"
                , div [ class "text-xl", style "opacity" ".4" ] [ text "Oops. Nothing here..." ]

                -- class="q-mt-xl" color="white" text-color="blue" unelevated to="/" label="Go Home" no-caps
                , a [ class "mt-8 bg-white text-blue-300", Route.href Route.Home ] [ text "Go Home" ]
                ]
            ]
    }
