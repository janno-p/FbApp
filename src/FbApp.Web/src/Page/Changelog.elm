module Page.Changelog exposing (view)

import Html exposing (Html, div, h1, span, text)
import Html.Attributes exposing (class)


-- MODEL


type alias LogEntry =
  { version : String
  , changes : List String
  }


log : List LogEntry
log =
  [ { version = "0.1.0"
    , changes =
      [ "Uuendatud rakenduse aluseks olevaid raamistikke"
      , "Lisatud Dapr runtime"
      , "Rohkem teenuseid!!!"
      , "Animatsioonid"
      , "Autentimine ja autoriseerimine JWT tokenitega"
      , "Rakendus arvestab ka olukorraga, kus aktiivne võistlus on veel määramata"
      , "Klientrakenduse üleviimine Elm-i peale"
      ]
    }
  , { version = "0.0.4"
    , changes =
      [ "Mängutulemuste kontrollimine v2 API-ga"
      , "Väljalangemismängude tulemuste näitamine"
      , "Üldise ennustuste punktitabeli näitamine"
      , "Mängude järjekorra parandamine"
      ]
    }
  , { version = "0.0.3"
    , changes =
      [ "Parandatud iluviga Chromes"
      , "Võistluste alguskuupäeva registreerimine"
      , "Mängutulemuste kontrollimine"
      , "Võistluste alguskuupäeva kontrollimine ennustuste registreerimisel"
      ]
    }
  , { version = "0.0.2"
    , changes =
      [ "Oma ennustuste vaade"
      , "Peale ennustuste registreerimist oma ennustuste vaate avamine"
      , "Olemasoleva ennustuste korral oma ennustuste vaate avamine"
      , "Juhusliku valiku tegemise võimalus"
      ]
    }
  , { version = "0.0.1"
    , changes =
      [ "Ennustuste registreerimine"
      ]
    }
  ]


-- VIEW


view : { title : String, content : Html msg }
view =
  { title = "Changelog"
  , content = div [ class "p-8" ] ( log |> List.map viewEntry )
  }


viewEntry : LogEntry -> Html msg
viewEntry entry =
    let
        headerHtml =
            h1 [] [ text ("Versioon " ++ entry.version) ]

        changesHtml =
            entry.changes
                |> List.map viewChange
    in
    div [ class "mb-1" ] ( headerHtml :: changesHtml )


viewChange : String -> Html msg
viewChange change =
    div [ class "flex flex-row space-x-2 items-center" ]
        [ span [ class "mdi mdi-check text-accent" ] []
        , span [] [ text change ]
        ]
