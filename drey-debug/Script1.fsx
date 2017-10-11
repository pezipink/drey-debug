#r "newtonsoft.json"
#r "c:\\repos\\drey-debug\\drey-debug\\bin\\debug\\fszmq.dll"
#r "System.Windows.Forms"
open fszmq
open System.Reflection
open fszmq
open fszmq.Context
open fszmq.Socket
open System
open Newtonsoft
open Newtonsoft.Json.Linq
open System.Collections.Generic
open System.Windows.Forms
// let x= Json.JsonConvert.DeserializeObject("""{"id":10, "bytes":[255,20,30], "strings":{"x":0, "y":1} }""") :?> JObject

// x.ToString()
// for y in x.["strings"] do
//     let j = y :?> JProperty
//     printfn "%A" j.Name
// for y in x.Values do
//     printfn "%A" y
    
// x.["id"].Value<int>()
// x.["bytes"].Values<byte>()
// x.["strings"].Value<JObject>().ToString()


let encode = string >> System.Text.Encoding.ASCII.GetBytes
let decode = System.Text.Encoding.ASCII.GetString

type MessageType =
    | Connect
    | Heartbeat
    | Data
    | Status
    | Universe
    | Debug 

type DebugMessageType = 
    | GetProgram
    | Run
    | Step
    | SetBreakpoint of int64
    | ClearBreakpoint of int64
    | InstallNewMessageHandler of (MessageType ->  string seq -> unit)


type Program =
   { stringTable : Map<int,string>
     program : byte array
     opcodes : Map<int, string * bool>}
   
let parseProgramResponse json =
    printfn "enterd parse"
    let json = Json.JsonConvert.DeserializeObject(json) :?> JObject
    let strings =
        json.["strings"]
        |> Seq.cast<JProperty>
        |> Seq.map(fun x -> (Int32.Parse x.Name,  x.Value.Value<string>() ))
        |> Map.ofSeq
    printf "strings %A" strings
    let program =
        json.["program"].Values<byte>() |> Seq.toArray

    let opcodes =
        json.["opcodes"]
        |> Seq.cast<JProperty>
        |> Seq.map(fun x -> 
                     let opcode = x.Name
                     let code = x.Value.["code"].Value<int>()
                     let extended = x.Value.["extended"].Value<bool>()
                     (code, (opcode, extended)))
        |> Map.ofSeq
        
        
    printfn "origram %A" program   
    { stringTable = strings; program = program; opcodes = opcodes }


let mutable program = { stringTable = Map.empty; program = [||]; opcodes = Map.empty }


let debugMsg()  =
    let mutable id = 0L
    fun (msgType:DebugMessageType) ->
        id <- id + 1L
        let ty =
            match msgType with
                | GetProgram -> "get-program"
                | _ -> ""
        sprintf """{"id":%i,"type":"%s"}""" id ty
    

type ServerMessage =
    | TEST

type ClientState = 
    { lastHeart : DateTime
      msgHandler : MessageType -> string seq -> Unit}

let decodeMessageType = function
    | 1uy -> Connect
    | 2uy -> Heartbeat
    | 3uy -> Data
    | 4uy -> Status
    | 5uy -> Universe
    | 6uy -> Debug
    | _ -> failwith "!"

let handleMessage (messageType:MessageType) (json:string seq) =
    match messageType with
    | Heartbeat -> ()
    | Debug ->
        printf "!"
        let s = Seq.toArray json
//        printf "!! %A" (s.[0].Substring(10))
        printf "!!! %A" (s.[1].Substring(0,10))
        let prog = parseProgramResponse s.[1]
        printfn "program is %A" prog
        program <- prog
    | _ -> printfn "recieved message %A %A" messageType json

////
//use context = new Context()
//use client  = dealer context
//
//Socket.setOption client (ZMQ.IDENTITY, (encode "__DEBUG__"))
//"tcp://localhost:5560" |> connect client 
//[|0x1uy|] |> send client
//let arr = ref [|[|0uy|]|]
//printfn "trying to get msg"
//client.TryGetInput(1L, arr)

use context = new Context()
use client  = dealer context

let createDebugMsg = debugMsg()

let mb = MailboxProcessor<DebugMessageType>.Start(fun inbox -> 
    Socket.setOption client (ZMQ.IDENTITY, (encode "__DEBUG__"))
    "tcp://localhost:5560"|> connect client 
    [|0x1uy|] |> send client
    let arr = ref [|[|0uy|]|]
    let rec loop state = async {
        let! msg = inbox.TryReceive(10)
        match msg with
        | Some (InstallNewMessageHandler h) ->
            return! loop { state with msgHandler = h }
        | Some msg ->
            let js = createDebugMsg msg
            printfn "sending message %s" js
            let client = [|0x6uy|] |> sendMore client
            js |> encode |> send client
            return! loop state
        | None ->
            // see what the server is saying
            try
                if client.TryGetInput(1L, arr) then
                    let msgType = (!arr).[0].[0]|> decodeMessageType
                    let json = !arr |>Seq.map decode 
                    state.msgHandler msgType json
                    return! loop state                              
                else
                    //send heartbeat
                    //printfn "sending hb"
    //                return! loop state
                    if DateTime.Now - state.lastHeart > TimeSpan.FromMilliseconds 100. then
                        [|0x2uy|] |> send client
                        return! loop { state with lastHeart = DateTime.Now }
                    else
                        return! loop state
            with
            | ex -> printf "%A" (ex.ToString())
                    
    }
    loop { lastHeart = DateTime.Now; msgHandler = handleMessage }
)


mb.Post(GetProgram)

mb.Post(InstallNewMessageHandler handleMessage)


program




let disassemble() =
    let mutable index = 0;
    let readByte() =
        let b = program.program.[index]
        index <- index + 1
        int b
    let readInt() =
        let mutable value = 0
        value <- readByte()
        value <- (value <<< 8) ||| readByte()
        value <- (value <<< 8) ||| readByte()
        value <- (value <<< 8)  ||| readByte()
        value

    let getString() =
        let i = readInt()
        program.stringTable.[i]

    let disass = ResizeArray<string*string*string>()    
    while index < program.program.Length do
        let (opcode,extended) = program.opcodes.[readByte()]
        let loc =  String.Format("{0:X}",index)
        if extended then
            match opcode with
                | "stvar"
                | "p_stvar"
                | "ldvals"
                | "ldvar" ->
                    disass.Add(loc, opcode, getString())
                | _ -> disass.Add(loc, opcode, readInt().ToString())    
        else
            disass.Add(loc, opcode, "")

    System.ComponentModel.BindingList(disass)

        
      
let f = new Form()    
let mainSplit = SplitContainer()
mainSplit.Dock <- DockStyle.Fill
let disassemblyGrid = DataGridView()
disassemblyGrid.KeyUp.Add(
    fun e ->
      if e.KeyCode = Keys.F9 then
        for r in disassemblyGrid.SelectedRows do
          r.DefaultCellStyle.BackColor <- System.Drawing.Color.Red
      else ())
disassemblyGrid.Dock <- DockStyle.Fill
disassemblyGrid.DataSource <- disassemble()
mainSplit.Panel1.Controls.Add disassemblyGrid
let rightSplit = SplitContainer()
rightSplit.Orientation <- Orientation.Horizontal
rightSplit.Dock <- DockStyle.Fill
mainSplit.Panel2.Controls.Add rightSplit
rightSplit.Panel1.Controls.Add ( TreeView(Dock = DockStyle.Fill ))
rightSplit.Panel2.Controls.Add ( TabControl(Dock = DockStyle.Fill))

f.Controls.Add(mainSplit)

f.Show()
