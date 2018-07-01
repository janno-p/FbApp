module FbApp.Core.Serialization

open FSharp.Reflection
open Newtonsoft.Json
open Newtonsoft.Json.Serialization
open System.IO
open System.Text

module Converters =
    // open Giraffe.Common
    open System
    open System.Collections.Generic

    type ListConverter () =
        inherit JsonConverter()
        override __.CanConvert (typ) =
            typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<list<_>>
        override __.WriteJson (writer, value, serializer) =
            let list = value :?> System.Collections.IEnumerable |> Seq.cast
            serializer.Serialize(writer, list)
        override __.ReadJson (reader, typ, _, serializer) =
            let itemType = typ.GetGenericArguments().[0]
            let collectionType = typedefof<IEnumerable<_>>.MakeGenericType(itemType)
            let collection = serializer.Deserialize(reader, collectionType) :?> System.Collections.IEnumerable |> Seq.cast
            let listType = typedefof<list<_>>.MakeGenericType(itemType)
            let cases = FSharpType.GetUnionCases(listType)
            let rec make = function
                | [] -> FSharpValue.MakeUnion(cases.[0], [||])
                | head::tail -> FSharpValue.MakeUnion(cases.[1], [| head; make tail |])
            collection |> Seq.toList |> make

    type OptionConverter () =
        inherit JsonConverter()
        override __.CanConvert (typ) =
            typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<option<_>>
        override __.WriteJson (writer, value, serializer) =
            let value =
                if value |> isNull then null else
                let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
                fields.[0]
            serializer.Serialize(writer, value)
        override __.ReadJson(reader, typ, _, serializer) =
            let innerType = typ.GetGenericArguments().[0]
            let innerType =
                if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType([|innerType|])
                else innerType
            let value = serializer.Deserialize(reader, innerType)
            let cases = FSharpType.GetUnionCases(typ)
            if value |> isNull then FSharpValue.MakeUnion(cases.[0], [||])
            else FSharpValue.MakeUnion(cases.[1], [|value|])

    type TupleArrayConverter () =
        inherit JsonConverter()
        override __.CanConvert (typ) =
            FSharpType.IsTuple(typ)
        override __.WriteJson (writer, value, serializer) =
            let values = FSharpValue.GetTupleFields(value)
            serializer.Serialize(writer, values)
        override __.ReadJson (reader, typ, _, serializer) =
            let advance = reader.Read >> ignore
            let deserialize typ = serializer.Deserialize(reader, typ)
            let itemTypes = FSharpType.GetTupleElements(typ)

            let readElements () =
                let rec read index acc =
                    match reader.TokenType with
                    | JsonToken.EndArray -> acc
                    | _ ->
                        let value = deserialize itemTypes.[index]
                        advance()
                        read (index + 1) (acc @ [value])
                advance()
                read 0 List.empty

            match reader.TokenType with
            | JsonToken.StartArray ->
                let values = readElements()
                FSharpValue.MakeTuple(values |> List.toArray, typ)
            | JsonToken.Null ->
                null
            | _ -> failwith "invalid token"

    type UnionCaseNameConverter () =
        inherit JsonConverter()
        override __.CanConvert (typ) =
            FSharpType.IsUnion(typ) || (typ.DeclaringType |> isNull |> not && FSharpType.IsUnion(typ.DeclaringType))
        override __.WriteJson (writer, value, serializer) =
            let typ = value.GetType()
            let caseInfo, fieldValues = FSharpValue.GetUnionFields(value, typ)
            writer.WriteStartObject()
            writer.WritePropertyName("case")
            writer.WriteValue(caseInfo.Name)
            writer.WritePropertyName("value")
            let value =
                match fieldValues.Length with
                | 0 -> null
                | 1 -> fieldValues.[0]
                | _ -> fieldValues :> obj
            serializer.Serialize(writer, value)
            writer.WriteEndObject()
        override __.ReadJson (reader, typ, _, serializer) =
            let typ = if FSharpType.IsUnion(typ) then typ else typ.DeclaringType

            let fail () = failwith "Invalid token!"

            let read (token: JsonToken) =
                if reader.TokenType = token then
                    let value = reader.Value
                    reader.Read() |> ignore
                    Some(value)
                else None

            let require v =
                match v with
                | Some(o) -> o
                | None -> fail()

            let readProp (n: string) =
                read JsonToken.PropertyName
                |> Option.map (fun v -> if (v :?> string) <> n then fail())

            read JsonToken.StartObject |> require |> ignore
            readProp "case" |> require |> ignore

            let case = read JsonToken.String |> require :?> string
            readProp "value" |> ignore

            let caseInfo = FSharpType.GetUnionCases(typ) |> Seq.find (fun c -> c.Name = case)
            let fields = caseInfo.GetFields()

            let args =
                match fields.Length with
                | 0 ->
                    read JsonToken.Null |> require |> ignore
                    [||]
                | 1 ->
                    [|serializer.Deserialize(reader, fields.[0].PropertyType)|]
                | _ ->
                    let tupleType = FSharpType.MakeTupleType(fields |> Seq.map (fun f -> f.PropertyType) |> Seq.toArray)
                    let tuple = serializer.Deserialize(reader, tupleType)
                    FSharpValue.GetTupleFields(tuple)

            FSharpValue.MakeUnion(caseInfo, args)

let serializer = JsonSerializer()
serializer.Converters.Add(Converters.TupleArrayConverter())
serializer.Converters.Add(Converters.OptionConverter())
serializer.Converters.Add(Converters.ListConverter())
serializer.Converters.Add(Converters.UnionCaseNameConverter())
serializer.ContractResolver <- CamelCasePropertyNamesContractResolver()

let eventType o =
    let typ = o.GetType()
    let unionType =
        if FSharpType.IsUnion(typ) then Some(typ)
        else if typ.DeclaringType |> isNull |> not && FSharpType.IsUnion(typ.DeclaringType) then Some(typ.DeclaringType)
        else None
    let typeName (typ: System.Type) =
        let name =
            let n = typ.FullName
            n.Substring(n.LastIndexOf(".") + 1)
        name.Replace("+Event", "")
    unionType
    |> Option.fold (fun _ ut ->
        let unionCase = FSharpValue.GetUnionFields(o, typ) |> fst
        sprintf "%s.%s" (typeName unionCase.DeclaringType) unionCase.Name
    ) typ.Name

let serialize o =
    use ms = new MemoryStream()
    (
        use writer = new JsonTextWriter(new StreamWriter(ms))
        serializer.Serialize(writer, o)
    )
    let data = ms.ToArray()
    (eventType o, data)

let deserialize (typ, _, data: byte array) =
    use ms = new MemoryStream(data)
    use reader = new JsonTextReader(new StreamReader(ms))
    serializer.Deserialize(reader, typ)

let deserializeOf<'T> (eventType, data) =
    deserialize (typeof<'T>, eventType, data) |> unbox<'T>

let deserializeType (data: byte array) =
    let json = Encoding.UTF8.GetString(data)
    JsonConvert.DeserializeObject<'T>(json)
