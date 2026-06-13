module Team exposing (estonianGenitiveName, estonianName, flagClass)

import Html
import Html.Attributes exposing (class)


estonianName : { a | tla : String, name : String } -> String
estonianName team =
    case team.tla of
        "ALG" ->
            "Alžeeria"

        "ARG" ->
            "Argentina"

        "AUS" ->
            "Austraalia"

        "AUT" ->
            "Austria"

        "BEL" ->
            "Belgia"

        "BIH" ->
            "Bosnia ja Hertsegoviina"

        "BRA" ->
            "Brasiilia"

        "CAN" ->
            "Kanada"

        "CPV" ->
            "Roheneemesaared"

        "COL" ->
            "Colombia"

        "CRO" ->
            "Horvaatia"

        "CUW" ->
            "Curaçao"

        "CZE" ->
            "Tšehhi"

        "COD" ->
            "Kongo DV"

        "ECU" ->
            "Ecuador"

        "EGY" ->
            "Egiptus"

        "ENG" ->
            "Inglismaa"

        "FRA" ->
            "Prantsusmaa"

        "GER" ->
            "Saksamaa"

        "GHA" ->
            "Ghana"

        "HAI" ->
            "Haiti"

        "IRN" ->
            "Iraan"

        "IRQ" ->
            "Iraak"

        "CIV" ->
            "Elevandiluurannik"

        "JPN" ->
            "Jaapan"

        "JOR" ->
            "Jordaania"

        "MEX" ->
            "Mehhiko"

        "MAR" ->
            "Maroko"

        "NED" ->
            "Holland"

        "NZL" ->
            "Uus-Meremaa"

        "NOR" ->
            "Norra"

        "PAN" ->
            "Panama"

        "PAR" ->
            "Paraguay"

        "POR" ->
            "Portugal"

        "QAT" ->
            "Katar"

        "KSA" ->
            "Saudi Araabia"

        "SCO" ->
            "Šotimaa"

        "SEN" ->
            "Senegal"

        "RSA" ->
            "Lõuna-Aafrika Vabariik"

        "KOR" ->
            "Lõuna-Korea"

        "ESP" ->
            "Hispaania"

        "SWE" ->
            "Rootsi"

        "SUI" ->
            "Šveits"

        "TUN" ->
            "Tuneesia"

        "TUR" ->
            "Türgi"

        "USA" ->
            "Ameerika Ühendriigid"

        "URY" ->
            "Uruguay"

        "UZB" ->
            "Usbekistan"

        _ ->
            team.name


estonianGenitiveName : { a | tla : String, name : String } -> String
estonianGenitiveName team =
    case team.tla of
        "ALG" ->
            "Alžeeria"

        "ARG" ->
            "Argentina"

        "AUS" ->
            "Austraalia"

        "AUT" ->
            "Austria"

        "BEL" ->
            "Belgia"

        "BIH" ->
            "Bosnia ja Hertsegoviina"

        "BRA" ->
            "Brasiilia"

        "CAN" ->
            "Kanada"

        "CPV" ->
            "Roheneemesaarte"

        "COL" ->
            "Colombia"

        "CRO" ->
            "Horvaatia"

        "CUW" ->
            "Curaçao"

        "CZE" ->
            "Tšehhi"

        "COD" ->
            "Kongo DV-i"

        "ECU" ->
            "Ecuadori"

        "EGY" ->
            "Egiptuse"

        "ENG" ->
            "Inglismaa"

        "FRA" ->
            "Prantsusmaa"

        "GER" ->
            "Saksamaa"

        "GHA" ->
            "Ghana"

        "HAI" ->
            "Haiti"

        "IRN" ->
            "Iraani"

        "IRQ" ->
            "Iraagi"

        "CIV" ->
            "Elevandiluuranniku"

        "JPN" ->
            "Jaapani"

        "JOR" ->
            "Jordaania"

        "MEX" ->
            "Mehhiko"

        "MAR" ->
            "Maroko"

        "NED" ->
            "Hollandi"

        "NZL" ->
            "Uus-Meremaa"

        "NOR" ->
            "Norra"

        "PAN" ->
            "Panama"

        "PAR" ->
            "Paraguay"

        "POR" ->
            "Portugali"

        "QAT" ->
            "Katari"

        "KSA" ->
            "Saudi Araabia"

        "SCO" ->
            "Šotimaa"

        "SEN" ->
            "Senegali"

        "RSA" ->
            "Lõuna-Aafrika Vabariigi"

        "KOR" ->
            "Lõuna-Korea"

        "ESP" ->
            "Hispaania"

        "SWE" ->
            "Rootsi"

        "SUI" ->
            "Šveitsi"

        "TUN" ->
            "Tuneesia"

        "TUR" ->
            "Türgi"

        "USA" ->
            "Ameerika Ühendriikide"

        "URY" ->
            "Uruguay"

        "UZB" ->
            "Usbekistani"

        _ ->
            estonianName team


flagClass : String -> List (Html.Attribute msg)
flagClass tla =
    case tla of
        "ALG" ->
            [ class "icon-[circle-flags--dz]" ]

        "ARG" ->
            [ class "icon-[circle-flags--ar]" ]

        "AUS" ->
            [ class "icon-[circle-flags--au]" ]

        "AUT" ->
            [ class "icon-[circle-flags--at]" ]

        "BEL" ->
            [ class "icon-[circle-flags--be]" ]

        "BIH" ->
            [ class "icon-[circle-flags--ba]" ]

        "BRA" ->
            [ class "icon-[circle-flags--br]" ]

        "CAN" ->
            [ class "icon-[circle-flags--ca]" ]

        "CPV" ->
            [ class "icon-[circle-flags--cv]" ]

        "COL" ->
            [ class "icon-[circle-flags--co]" ]

        "CRO" ->
            [ class "icon-[circle-flags--hr]" ]

        "CUW" ->
            [ class "icon-[circle-flags--cw]" ]

        "CZE" ->
            [ class "icon-[circle-flags--cz]" ]

        "COD" ->
            [ class "icon-[circle-flags--cd]" ]

        "ECU" ->
            [ class "icon-[circle-flags--ec]" ]

        "EGY" ->
            [ class "icon-[circle-flags--eg]" ]

        "ENG" ->
            [ class "icon-[circle-flags--gb-eng]" ]

        "FRA" ->
            [ class "icon-[circle-flags--fr]" ]

        "GER" ->
            [ class "icon-[circle-flags--de]" ]

        "GHA" ->
            [ class "icon-[circle-flags--gh]" ]

        "HAI" ->
            [ class "icon-[circle-flags--ht]" ]

        "IRN" ->
            [ class "icon-[circle-flags--ir]" ]

        "IRQ" ->
            [ class "icon-[circle-flags--iq]" ]

        "CIV" ->
            [ class "icon-[circle-flags--ci]" ]

        "JPN" ->
            [ class "icon-[circle-flags--jp]" ]

        "JOR" ->
            [ class "icon-[circle-flags--jo]" ]

        "MEX" ->
            [ class "icon-[circle-flags--mx]" ]

        "MAR" ->
            [ class "icon-[circle-flags--ma]" ]

        "NED" ->
            [ class "icon-[circle-flags--nl]" ]

        "NZL" ->
            [ class "icon-[circle-flags--nz]" ]

        "NOR" ->
            [ class "icon-[circle-flags--no]" ]

        "PAN" ->
            [ class "icon-[circle-flags--pa]" ]

        "PAR" ->
            [ class "icon-[circle-flags--py]" ]

        "POR" ->
            [ class "icon-[circle-flags--pt]" ]

        "QAT" ->
            [ class "icon-[circle-flags--qa]" ]

        "KSA" ->
            [ class "icon-[circle-flags--sa]" ]

        "SCO" ->
            [ class "icon-[circle-flags--gb-sct]" ]

        "SEN" ->
            [ class "icon-[circle-flags--sn]" ]

        "RSA" ->
            [ class "icon-[circle-flags--za]" ]

        "KOR" ->
            [ class "icon-[circle-flags--kr]" ]

        "ESP" ->
            [ class "icon-[circle-flags--es]" ]

        "SWE" ->
            [ class "icon-[circle-flags--se]" ]

        "SUI" ->
            [ class "icon-[circle-flags--ch]" ]

        "TUN" ->
            [ class "icon-[circle-flags--tn]" ]

        "TUR" ->
            [ class "icon-[circle-flags--tr]" ]

        "USA" ->
            [ class "icon-[circle-flags--us]" ]

        "URY" ->
            [ class "icon-[circle-flags--uy]" ]

        "UZB" ->
            [ class "icon-[circle-flags--uz]" ]

        _ ->
            []
