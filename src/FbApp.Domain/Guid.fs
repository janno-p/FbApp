[<RequireQualifiedAccess>]
module FbApp.Domain.Guid

open System
open System.Security.Cryptography
open System.Text

let private swapBytes (guid: byte[]) (left: int) (right: int) =
    let temp = guid.[left]
    guid.[left] <- guid.[right]
    guid.[right] <- temp

let private swapByteOrder (guid: byte[]) =
    swapBytes guid 0 3
    swapBytes guid 1 2
    swapBytes guid 4 5
    swapBytes guid 6 7

let createDeterministicGuidFromBytes (namespaceId: Guid) nameBytes =
    let namespaceBytes = namespaceId.ToByteArray()
    swapByteOrder namespaceBytes

    use algorithm = SHA1.Create()
    algorithm.TransformBlock(namespaceBytes, 0, namespaceBytes.Length, null, 0) |> ignore
    algorithm.TransformFinalBlock(nameBytes, 0, nameBytes.Length) |> ignore

    let hash = algorithm.Hash

    let newGuid = Array.zeroCreate<byte> 16
    Array.Copy(hash, 0, newGuid, 0, 16)

    newGuid.[6] <- (newGuid.[6] &&& (byte 0x0F)) ||| ((byte 5) <<< 4)
    newGuid.[8] <- (newGuid.[8] &&& (byte 0x3F)) ||| (byte 0x80)
    swapByteOrder newGuid

    Guid(newGuid)

let createDeterministicGuid (namespaceId: Guid) (name: string) =
    if name |> isNull then failwith "Argument null exception 'name'"
    let nameBytes = Encoding.UTF8.GetBytes(name)
    createDeterministicGuidFromBytes namespaceId nameBytes
