[<RequireQualifiedAccess>]
module FbApp.Server.Prediction

open System

type State = unit

let initialState =
    ()

[<CLIMutable>]
type FixtureResultRegistrationInput =
    {
        Id: int64
        Result: string
    }

[<CLIMutable>]
type QualifiersRegistrationInput =
    {
        RoundOf16: int64 array
        RoundOf8: int64 array
        RoundOf4: int64 array
        RoundOf2: int64 array
    }

[<CLIMutable>]
type PredictionRegistrationInput =
    {
        CompetitionId: Guid
        Fixtures: FixtureResultRegistrationInput[]
        Qualifiers: QualifiersRegistrationInput
        Winner: int64
    }

type FixtureResult =
    | HomeWin
    | Tie
    | AwayWin

type FixtureResultRegistration =
    {
        Id: int64
        Result: FixtureResult
    }

type QualifiersRegistration =
    {
        RoundOf16: int64 list
        RoundOf8: int64 list
        RoundOf4: int64 list
        RoundOf2: int64 list
    }

type PredictionRegistration =
    {
        Name: string
        Email: string
        CompetitionId: Guid
        Fixtures: FixtureResultRegistration list
        Qualifiers: QualifiersRegistration
        Winner: int64
    }

type Command =
    | Register of PredictionRegistrationInput * string * string

type Event =
    | Registered of PredictionRegistration

let decide: State -> Command -> Result<Event list,unit> =
    (fun _ -> function
        | Register (input, name, email) ->
            let mapResult = function
                | "HOME" -> HomeWin
                | "TIE" -> Tie
                | "AWAY" -> AwayWin
                | other -> failwithf "Invalid result value: %s" other
            let registration =
                {
                    Name = name
                    Email = email
                    CompetitionId = input.CompetitionId
                    Fixtures =
                        input.Fixtures
                        |> Seq.map (fun x ->
                            {
                                Id = x.Id
                                Result = mapResult x.Result
                            })
                        |> Seq.toList
                    Qualifiers =
                        {
                            RoundOf16 =
                                input.Qualifiers.RoundOf16
                                |> List.ofArray
                            RoundOf8 =
                                input.Qualifiers.RoundOf8
                                |> List.ofArray
                            RoundOf4 =
                                input.Qualifiers.RoundOf4
                                |> List.ofArray
                            RoundOf2 =
                                input.Qualifiers.RoundOf2
                                |> List.ofArray
                        }
                    Winner = input.Winner
                }
            Ok([Registered registration])
    )

let evolve: State -> Event -> State =
    (fun _ -> function
        | Registered _ -> ()
    )
