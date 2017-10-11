// Learn more about F# at http://fsharp.org
// See the 'F# Tutorial' project for more help.
open fszmq


[<EntryPoint>]
let main argv = 
    printfn "libzmq version: %A" ZMQ.version    
    printfn "%A" argv
    0 // return an integer exit code
