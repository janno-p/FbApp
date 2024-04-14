module internal FbApp.Modules.WebApp.Layout

open Giraffe.ViewEngine


type UserModel = {
    Name: string
    Picture: string
    HasAdminRole: bool
}


type PageModel = {
    Title: string option
    CompetitionName: string option
    User: UserModel option
    Content: XmlNode list
}


let private viewUser (user: UserModel option) : XmlNode list =
    match user with
    | Some(user) ->
        [
            yield div [_class "px-2 flex flex-row flex-nowrap items-center grow-0"] [
                img [_alt "Avatar"; _class "w-10 h-10 rounded-full"; _src user.Picture]
                div [_class "pl-1 font-medium"] [encodedText user.Name]
            ]

            if user.HasAdminRole then
                yield a [_href Routes.Changelog; _class "text-white grow-0 h-8 flex flex-row flex-nowrap items-center justify-center"] [
                    span [_class "material-symbols-outlined"] [encodedText "manufacturing"]
                    span [_class "whitespace-nowrap"] [encodedText "Ava kontrollpaneel"]
                ]

            yield a [_href Routes.Logout; _class "text-white grow-0 h-8 flex flex-row flex-nowrap items-center justify-center"] [
                span [_class "material-symbols-outlined"] [encodedText "logout"]
                span [_class "whitespace-nowrap"] [encodedText "Logi välja"]
            ]
        ]
    | None ->
        [
            a [_href Routes.GoogleLogin; _class "text-white grow-0 w-8 h-8 flex items-center justify-center"; _title "Logi sisse Google kontoga"] [
                span [_class "material-symbols-outlined"] [encodedText "login"]
            ]
        ]


let private viewSiteToolbar competitionName user =
    nav [_class "glossy bg-sky-600 flex flex-row flex-nowrap items-center text-white px-4 py-1.5 gap-2"] [
        yield a [_href Routes.Home; _class "text-white grow-0"] [
            span [_class "material-symbols-outlined !text-4xl"] [encodedText "sports_and_outdoors"]
        ]
        yield a [_href Routes.Home; _class "flex flex-col grow text-white gap-2"] [
            span [_class "grow-0 text-xl leading-4"] [encodedText "Ennustusmäng"]
            div [_class "grow uppercase text-sm leading-4"] [encodedText (competitionName |> Option.defaultValue "")]
        ]
        yield a [_href Routes.Changelog; _class "text-white grow-0 w-8 h-8 flex items-center justify-center"; _title "Versioonide ajalugu"] [
            span [_class "material-symbols-outlined"] [encodedText "checklist"]
        ]
        yield! viewUser user
    ]


let private viewFooter =
    footer [] []


let ``default`` (page: PageModel) =
    html [_lang "en"] [
        head [] [
            meta [_charset "UTF-8"]
            link [_rel "icon"; _type "image/svg+xml"; _href "/favicon.svg"]
            link [_rel "stylesheet"; _href "/css/app.css"]
            link [_rel "stylesheet"; _href "https://fonts.googleapis.com/css2?family=Material+Symbols+Outlined:opsz,wght,FILL,GRAD@24,400,0,0"]
            meta [_name "viewport"; _content "width=device-width, initial-scale=1.0"]
            title [] [encodedText (page.Title |> Option.map (fun x -> $"%s{x} - FbApp") |> Option.defaultValue "FbApp")]
        ]
        body [] [
            yield noscript [] [encodedText "This is your fallback content in case JavaScript fails to load."]
            yield viewSiteToolbar page.CompetitionName page.User
            yield! page.Content
            yield viewFooter
        ]
    ]
