module FbApp.Modules.WebApp.Views

open Giraffe.ViewEngine


type UserModel = {
    Name: string
    Picture: string
    HasAdminRole: bool
}


type PageModel = {
    PageTitle: string option
    CompetitionName: string option
    User: UserModel option
}


let viewUser (user: UserModel option) : XmlNode list =
    match user with
    | Some(user) ->
        [
            yield div [_class "px-2 flex flex-row flex-nowrap items-center"] [
                img [_alt "Avatar"; _class "w-10 h-10 rounded-full"; _src user.Picture]
                div [_class "pl-1 font-medium"] [encodedText user.Name]
            ]

            if user.HasAdminRole then
                yield a [_href Routes.Changelog; _class "text-white"] [
                    span [_class "material-symbols-outlined"] [encodedText "manufacturing"]
                    span [] [encodedText "Ava kontrollpaneel"]
                ]

            yield a [_href Routes.Logout; _class "text-white"] [
                span [_class "material-symbols-outlined"] [encodedText "logout"]
                span [] [encodedText "Logi välja"]
            ]
        ]
    | None ->
        [
            a [_href Routes.GoogleLogin; _class "text-white"; _title "Logi sisse Google kontoga"] [
                div [_class "border border-white rounded-full w-8 h-8 flex items-center justify-center"] [
                    span [_class "material-symbols-outlined"] [encodedText "login"]
                ]
            ]
        ]


let viewSiteToolbar competitionName user =
    nav [_class "glossy bg-sky-600 flex flex-row flex-nowrap items-center text-white px-4 py-1.5 gap-2"] [
        a [_href Routes.Home; _class "text-white grow-0"] [
            span [_class "material-symbols-outlined !text-4xl"] [encodedText "sports_and_outdoors"]
        ]
        a [_href Routes.Home; _class "flex flex-col grow text-white gap-2"] [
            span [_class "grow-0 text-xl leading-4"] [encodedText "Ennustusmäng"]
            div [_class "grow uppercase text-sm leading-4"] [encodedText (competitionName |> Option.defaultValue "")]
        ]
        div [_class "grow-0 py-0.5"] (viewUser user)
    ]


let viewFooter =
    footer [] []


let defaultLayout (page: PageModel) (content: XmlNode list) =
    html [_lang "en"] [
        head [] [
            meta [_charset "UTF-8"]
            link [_rel "icon"; _type "image/svg+xml"; _href "/favicon.svg"]
            link [_rel "stylesheet"; _href "/css/app.css"]
            link [_rel "stylesheet"; _href "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0,0"]
            meta [_name "viewport"; _content "width=device-width, initial-scale=1.0"]
            title [] [encodedText (page.PageTitle |> Option.map (fun x -> $"%s{x} - FbApp") |> Option.defaultValue "FbApp")]
        ]
        body [] [
            yield noscript [] [encodedText "This is your fallback content in case JavaScript fails to load."]
            yield viewSiteToolbar page.CompetitionName page.User
            yield! content
            yield viewFooter
        ]
    ]


let viewHome = [
    h1 [] [encodedText "Home!"]
]
