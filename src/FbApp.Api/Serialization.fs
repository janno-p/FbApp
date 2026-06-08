module FbApp.Api.Serialization

open FSharp.Reflection
open System
open System.IO
open System.Text
open System.Text.Json
open System.Threading

let eventType o =
    let typ = o.GetType()
    let unionType =
        if FSharpType.IsUnion(typ) then Some(typ)
        else if typ.DeclaringType |> isNull |> not && FSharpType.IsUnion(typ.DeclaringType) then Some(typ.DeclaringType)
        else None
    let typeName (typ: Type) =
        let name =
            let n = typ.FullName
            n.Substring(n.LastIndexOf(".") + 1)
        name.Replace("+Event", "")
    unionType
    |> Option.fold (fun _ _ ->
        let unionCase = FSharpValue.GetUnionFields(o, typ) |> fst
        $"%s{typeName unionCase.DeclaringType}.%s{unionCase.Name}"
    ) typ.Name

let serialize (jsonOptions: JsonSerializerOptions) o =
    let bytes =
        JsonSerializer.SerializeToUtf8Bytes(o, jsonOptions)
        |> ReadOnlyMemory<_>
    eventType o, bytes

let deserialize (jsonOptions: JsonSerializerOptions) (typ, _, data: ReadOnlyMemory<byte>) =
    JsonSerializer.Deserialize(data.Span, typ, jsonOptions)

let deserializeOf<'T> jsonOptions (eventType, data) =
    deserialize jsonOptions (typeof<'T>, eventType, data) |> unbox<'T>

let deserializeType (jsonOptions: JsonSerializerOptions) (data: ReadOnlyMemory<byte>) =
    let json = Encoding.UTF8.GetString(data.ToArray())
    JsonSerializer.Deserialize<'T>(json, jsonOptions)
