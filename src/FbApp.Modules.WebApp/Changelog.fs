module internal FbApp.Modules.WebApp.Changelog

open Giraffe.ViewEngine

type private LogEntry = {
    Version: string
    Changes: string list
}

let private log: LogEntry list = [
    {
        Version = "0.2.0"
        Changes = [
            "Ümberlülitus modulaarse arhitektuuri peale"
        ]
    }
    {
        Version = "0.1.0"
        Changes = [
            "Uuendatud rakenduse aluseks olevaid raamistikke"
            "Lisatud Dapr runtime"
            "Rohkem teenuseid!!!"
            "Animatsioonid"
            "Autentimine ja autoriseerimine JWT tokenitega"
            "Rakendus arvestab ka olukorraga, kus aktiivne võistlus on veel määramata"
            "Klientrakenduse üleviimine Elm-i peale"
        ]
    }
    {
        Version = "0.0.4"
        Changes = [
            "Mängutulemuste kontrollimine v2 API-ga"
            "Väljalangemismängude tulemuste näitamine"
            "Üldise ennustuste punktitabeli näitamine"
            "Mängude järjekorra parandamine"
        ]
    }
    {
        Version = "0.0.3"
        Changes = [
            "Parandatud iluviga Chromes"
            "Võistluste alguskuupäeva registreerimine"
            "Mängutulemuste kontrollimine"
            "Võistluste alguskuupäeva kontrollimine ennustuste registreerimisel"
        ]
    }
    {
        Version = "0.0.2"
        Changes = [
            "Oma ennustuste vaade"
            "Peale ennustuste registreerimist oma ennustuste vaate avamine"
            "Olemasoleva ennustuste korral oma ennustuste vaate avamine"
            "Juhusliku valiku tegemise võimalus"
        ]
    }
    {
        Version = "0.0.1"
        Changes = [
            "Ennustuste registreerimine"
        ]
    }
]

let private viewChange change =
    div [_class "flex flex-row space-x-2 items-center"] [
        span [_class "material-symbols-outlined"] [encodedText "check"]
        span [] [encodedText change]
    ]

let private viewEntry entry =
    div [_class "mb-1"] [
        yield h1 [] [encodedText $"Versioon %s{entry.Version}"]
        yield! entry.Changes |> List.map viewChange
    ]

let view = {|
    Title = "Changelog"
    Content = div [_class "p-8"] (log |> List.map viewEntry)
|}
