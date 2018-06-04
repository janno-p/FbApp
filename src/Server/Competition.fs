[<RequireQualifiedAccess>]
module FbApp.Server.Competition

type State = unit

let initialState =
    ()

type Created =
    {
        Description: string
        ExternalSource: int64
    }

type Command =
    | Create of string * int64

type Event =
    | Created of Created

let decide: State -> Command -> Result<Event list,unit> =
    (fun state -> function
        | Create (description, externalSource) ->
            Ok([Created { Description = description; ExternalSource = externalSource }])
    )

let evolve: State -> Event -> State =
    (fun state -> function
        | Created args -> ()
    )
