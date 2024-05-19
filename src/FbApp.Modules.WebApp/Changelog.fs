[<RequireQualifiedAccess>]
module internal FbApp.Modules.WebApp.Changelog

open Oxpecker.ViewEngine

open Types

type private LogEntry = {
    Version: string
    Changes: HtmlElement list
}

let private log: LogEntry list = [
    {
        Version = "0.2.0"
        Changes = [
            __() { "Mikroteenused asendatud modulaarse arhitektuuriga" }
            __() {
                a(class' = "text-primary underline hover:no-underline", href = "https://github.com/giraffe-fsharp/Giraffe", target = "_blank") { "Giraffe" }
                " asemel kasutusele võetud "
                a(class' = "text-primary underline hover:no-underline", href = "https://github.com/Lanayx/Oxpecker", target = "_blank") { "Oxpecker" }
            }
            __() {
                "Kasutusele võetud "
                a(class' = "text-primary underline hover:no-underline", href = "https://htmx.org/", target = "_blank") { "HTMX" }
            }
        ]
    }
    {
        Version = "0.1.0"
        Changes = [
            __() { "Uuendatud rakenduse aluseks olevaid raamistikke" }
            __() { "Lisatud Dapr runtime" }
            __() { "Rohkem teenuseid!!!" }
            __() { "Animatsioonid" }
            __() { "Autentimine ja autoriseerimine JWT tokenitega" }
            __() { "Rakendus arvestab ka olukorraga, kus aktiivne võistlus on veel määramata" }
            __() { "Klientrakenduse üleviimine Elm-i peale" }
        ]
    }
    {
        Version = "0.0.4"
        Changes = [
            __() { "Mängutulemuste kontrollimine v2 API-ga" }
            __() { "Väljalangemismängude tulemuste näitamine" }
            __() { "Üldise ennustuste punktitabeli näitamine" }
            __() { "Mängude järjekorra parandamine" }
        ]
    }
    {
        Version = "0.0.3"
        Changes = [
            __() { "Parandatud iluviga Chromes" }
            __() { "Võistluste alguskuupäeva registreerimine" }
            __() { "Mängutulemuste kontrollimine" }
            __() { "Võistluste alguskuupäeva kontrollimine ennustuste registreerimisel" }
        ]
    }
    {
        Version = "0.0.2"
        Changes = [
            __() { "Oma ennustuste vaade" }
            __() { "Peale ennustuste registreerimist oma ennustuste vaate avamine" }
            __() { "Olemasoleva ennustuste korral oma ennustuste vaate avamine" }
            __() { "Juhusliku valiku tegemise võimalus" }
        ]
    }
    {
        Version = "0.0.1"
        Changes = [
            __() { "Ennustuste registreerimine" }
        ]
    }
]

let private viewChange (change: HtmlElement) =
    div(class' = "flex flex-row space-x-2 items-center") {
        span (class' = "icon-[mdi--check]")
        span() { change }
    }

let private viewEntry entry =
    div(class' = "mb-4") {
        h1(class' = "font-bold text-lg mb-2") { $"Versioon %s{entry.Version}" }
        for change in entry.Changes do
            viewChange change
    }

let view: View = {
    Title = "Changelog"
    Content = div(class' = "p-8") {
        for entry in log do
            viewEntry entry
    }
}
