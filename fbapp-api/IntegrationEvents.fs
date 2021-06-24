namespace FbApp.Api


open Microsoft.FSharp.Reflection
open Newtonsoft.Json
open System


[<AllowNullLiteral>]
type IValue =
    interface
        abstract member GetValue: unit -> obj
        abstract member SetValue: obj -> unit
    end


[<AllowNullLiteral>]
type Value<'t> () =
    member val Value = Unchecked.defaultof<'t> with get, set
    interface IValue with
        member this.GetValue() =
            box this.Value
        member this.SetValue(value: obj) =
            this.Value <- unbox value


type OptionConverter () =
    inherit JsonConverter()

    override _.CanConvert (typ) =
        typ.IsGenericType && typ.GetGenericTypeDefinition() = typedefof<option<_>>

    override _.WriteJson (writer, value, serializer) =
        let value =
            if value |> isNull then null else
            let innerType = value.GetType().GetGenericArguments().[0]
            let valueWrapperType = (typedefof<Value<_>>).MakeGenericType([|innerType|])
            let valueWrapper: IValue = Activator.CreateInstance(valueWrapperType) |> unbox
            let _, fields = FSharpValue.GetUnionFields(value, value.GetType())
            valueWrapper.SetValue(fields.[0])
            valueWrapper
        serializer.Serialize(writer, value)

    override __.ReadJson(reader, typ, _, serializer) =
        let innerType = typ.GetGenericArguments().[0]
        let innerType =
            if innerType.IsValueType then typedefof<Nullable<_>>.MakeGenericType([|innerType|])
            else innerType
        let cases = FSharpType.GetUnionCases(typ)
        if reader.TokenType = JsonToken.Null then
            FSharpValue.MakeUnion(cases.[0], [||])
        else
            let valueType = (typedefof<Value<_>>).MakeGenericType([|innerType|])
            let value: IValue = serializer.Deserialize(reader, valueType) |> unbox
            FSharpValue.MakeUnion(cases.[1], [|value.GetValue()|])


type FixtureDto = {
    FixtureId: int64
    CompetitionId: int64
    HomeTeamId: int64 option
    AwayTeamId: int64 option
    UtcDate: DateTimeOffset
    Stage: string
    Status: string
    FullTime: (int * int) option
    HalfTime: (int * int) option
    ExtraTime: (int * int) option
    Penalties: (int * int) option
    Winner: string option
    Duration: string
    }


type FixturesUpdatedIntegrationEvent = {
    Fixtures: FixtureDto[]
    }
