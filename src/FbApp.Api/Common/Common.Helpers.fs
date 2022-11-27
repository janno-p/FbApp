module FbApp.Common.Helpers


let excludeLastName (name: string) =
    match name.LastIndexOf ' ' with
    | -1 -> name
    | index -> name.Substring(0, index)
