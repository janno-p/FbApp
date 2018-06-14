// Learn more about F# at http://fsharp.org

open System

AppDomain.CurrentDomain.ProcessExit.Add (fun e -> printfn "Process exiting...")

[<EntryPoint>]
let main argv =
    printfn "Hello World from F#!"
    let mutable ``done`` = false
    Console.CancelKeyPress.Add (fun e -> ``done`` <- true; e.Cancel <- true)
    while not ``done`` do
        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(1.0))
    System.Threading.Thread.Sleep(TimeSpan.FromSeconds(10.0))
    0 // return an integer exit code
