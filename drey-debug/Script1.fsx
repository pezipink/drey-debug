#r "newtonsoft.json"
#r "c:\\repos\\drey-debug\\drey-debug\\bin\\debug\\fszmq.dll"
#r "System.Windows.Forms"
#r "Microsoft.VisualBasic"

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

let encode = string >> System.Text.Encoding.ASCII.GetBytes
let decode = System.Text.Encoding.ASCII.GetString

let fromHex n = Int32.Parse(n, System.Globalization.NumberStyles.HexNumber)
let toHex (n:int) = String.Format("{0:X}",n)


type Program =
   { stringTable : Map<int,string>
     program : byte array
     opcodes : Map<int, string * bool>}
   with static member Blank = { stringTable = Map.empty
                                program = [||]
                                opcodes = Map.empty }

type MessageType =
    | Connect
    | Heartbeat
    | Data
    | Status
    | Universe
    | Debug 

type DebugMessageType = 
    | GetProgram
    | Step
    | Run
    | SetBreakpoint of int32
    | ClearBreakpoint of int32
    | InstallNewMessageHandler of (MessageType ->  string seq -> UI ->  unit)
    | InstallNewUI of UI
    | ReConnect

and
    UI =
    {
        mutable program : Program
        mutable mb : MailboxProcessor<DebugMessageType>
        mutable disassGrid : DataGridView
        mutable tabs : TabControl
        mutable jsonArea : RichTextBox 
        mutable objectsNode : TreeNode
        mutable machinesNode : TreeNode
        mutable gotoInstruction : string -> unit
    }
    with
    static member Blank = 
        { mb = Unchecked.defaultof<_>; program = Unchecked.defaultof<_>; disassGrid = null; tabs = null; jsonArea = null 
          objectsNode = null; machinesNode = null; gotoInstruction = Unchecked.defaultof<_>}
    member this.disassemble(newProgram:Program) =
        
        this.program <- newProgram
        let mutable index = 0;
        let readByte() =
            let b = this.program.program.[index]
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
            this.program.stringTable.[i]
                
        let disass = ResizeArray<string*string*string>()    
        while index < this.program.program.Length do
            let (opcode,extended) = this.program.opcodes.[readByte()]
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

        this.disassGrid.DataSource <- System.ComponentModel.BindingList(disass)
        this.disassGrid.Columns.[0].HeaderText <- "Address"
        this.disassGrid.Columns.[1].HeaderText <- "Opcode"
        this.disassGrid.Columns.[2].HeaderText <- "Operand"

   
let parseProgramResponse (json:JObject) =
    printfn "enterd parse"

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
        
        
    { stringTable = strings; program = program; opcodes = opcodes }


let debugMsg()  =
    let mutable id = 0L
    fun (msgType:DebugMessageType) ->
        id <- id + 1L
        let mutable extra = ""
        let ty =
            match msgType with
                | GetProgram -> "get-program"
                | SetBreakpoint address ->
                    extra <- sprintf ", \"address\":%i" address
                    "set-breakpoint"
                | ClearBreakpoint address ->
                    extra <- sprintf ", \"address\":%i" address
                    "clear-breakpoint"
                | Step -> "step" 
                | Run -> "run"                    
                | _ -> ""
        sprintf """{"id":%i,"type":"%s" %s}""" id ty extra
    

type ServerMessage =
    | TEST

type ClientState = 
    { lastHeart : DateTime
      ui : UI
      msgHandler : MessageType -> string seq -> UI -> Unit}

let decodeMessageType = function
    | 1uy -> Connect
    | 2uy -> Heartbeat
    | 3uy -> Data
    | 4uy -> Status
    | 5uy -> Universe
    | 6uy -> Debug
    | _ -> failwith "!"

let mutable f = new Form()

let handleMessage  (messageType:MessageType) (json:string seq) (ui:UI) =
    match messageType with
    | Heartbeat -> ()
    | Debug ->
        let s = Seq.toArray json
        printfn "!! %A" (s.[0])

        let jo = Json.JsonConvert.DeserializeObject(s.[1]) :?> JObject
        ui.jsonArea.Text <- Json.JsonConvert.SerializeObject(jo, Json.Formatting.Indented)
        match jo.["type"].Value<string>() with
            | "get-program" ->
                let prog = parseProgramResponse jo
                printfn "program is %A" prog
                f.BeginInvoke(new MethodInvoker( fun _ -> ui.disassemble prog )) |> ignore

            | "set-breakpoint" ->
                let address = jo.["address"].Value<int>() |> toHex
                let mutable i = 0
                while i < ui.disassGrid.Rows.Count do
                    if ui.disassGrid.Rows.[i].Cells.[0].Value.ToString() = address then
                        ui.disassGrid.Rows.[i].DefaultCellStyle.BackColor <- System.Drawing.Color.Red
                        i <- ui.disassGrid.Rows.Count                                                
                    i <- i + 1
            | "clear-breakpoint" ->
                let address = jo.["address"].Value<int>() |> toHex
                let mutable i = 0
                while i < ui.disassGrid.Rows.Count do
                    if ui.disassGrid.Rows.[i].Cells.[0].Value.ToString() = address then
                        ui.disassGrid.Rows.[i].DefaultCellStyle.BackColor <- System.Drawing.Color.White
                        i <- ui.disassGrid.Rows.Count                                                
                    i <- i + 1
            | "announce" ->
                f.BeginInvoke(new MethodInvoker( fun _ -> 
                    ui.objectsNode.Nodes.Clear()
                    printfn "got announce message, processing.. %A" jo
                    for go in jo.["gameobjects"] do
                        let n = TreeNode(go.["id"].ToString(), Tag = go)
                        ui.objectsNode.Nodes.Add(n) |> ignore

                    let newMachines = 
                        jo.["machines"] 
                        |> Seq.mapi(fun i m -> 
                            ((sprintf "machine%i" i), m))
                        |> Seq.toList
                    
                    let pc = (snd newMachines.[newMachines.Length - 1]).["pc"].Value<int>() |> fun i -> toHex (i+1)

                    printfn "new pc is %s" pc
                    ui.gotoInstruction pc

//                    let existing = 
//                        ui.machinesNode.Nodes 
//                        |> Seq.cast<TreeNode>
//                        |> Seq.map(fun n -> n.Name, n)
//                        |> Map.ofSeq
////                        
//                    for kvp in newMachines do
//                        if existing.ContainsKey kvp.Key then
//                            existing.[kvp.Key] <- kvp.Value
                        
                        

                )) |> ignore
                
                
            | _ -> printfn "%A" jo


    | _ -> printfn "recieved message %A %A" messageType json

type ObjectContext =
    { key : string
      kind : string
      data : string }

let createUi (inbox:MailboxProcessor<DebugMessageType>) =
    if f.IsDisposed |> not then f.Dispose()
    f <- new Form(Height=700, Width = 1400)
    f.Text <- "Drey VM - Remote Debugger"
    f.Icon <- new Drawing.Icon("c:\\repos\\frog-blog\\favicon.ico")
    let status = new StatusBar()
    let mainSplit = new SplitContainer()
    mainSplit.Dock <- DockStyle.Fill

    //DISASSEMBLY GRID

    let disassemblyGrid = new DataGridView()   
    disassemblyGrid.SelectionMode <- DataGridViewSelectionMode.FullRowSelect
    let gotoInstruction hexString =
        try
            let mutable i = 0 
            while i < disassemblyGrid .Rows.Count do                             
            if disassemblyGrid.Rows.[i].Cells.[0].Value.ToString() = hexString  then
                disassemblyGrid.CurrentCell <- disassemblyGrid.Rows.[i].Cells.[0]
                disassemblyGrid.SelectedRows.Clear()
                disassemblyGrid.SelectedRows.Insert(0,disassemblyGrid.Rows.[i])
                i <- disassemblyGrid.Rows.Count                                                
            i <- i + 1
        with
        | _ -> ()
    disassemblyGrid.Dock <- DockStyle.Fill
    mainSplit.Panel1.Controls.Add disassemblyGrid

    // CORE SPLITTER 
    let rightSplit = new SplitContainer()
    rightSplit.Orientation <- Orientation.Horizontal
    rightSplit.Dock <- DockStyle.Fill
    mainSplit.Panel2.Controls.Add rightSplit
    
    // TREE SPLITTER
    let treeSplit = new SplitContainer()
    treeSplit.Orientation <- Orientation.Vertical    
    treeSplit.Dock <- DockStyle.Fill
    // MAIN TREE
    let tree = new TreeView(Dock = DockStyle.Fill )
    let rootNode = new TreeNode("Drey")
    let machinesNode = new TreeNode("Machines")
    let machine0 = new TreeNode("Machine 0")
    rootNode.Nodes.Add(machinesNode) |> ignore
    machinesNode.Expand()
    machinesNode.Nodes.Add machine0 |> ignore
    tree.Nodes.Add rootNode      |> ignore
    
    let universeNode = new TreeNode("Universe")
    let gameObjectsNode = new TreeNode("Game Objects")
    let locationsNode = new TreeNode("Locations")
    let locationRefsNode = new TreeNode("Location Refs")

    rootNode.Expand()
    machine0.Expand()
    universeNode.Nodes.Add gameObjectsNode  |> ignore
    universeNode.Nodes.Add locationsNode    |> ignore
    universeNode.Nodes.Add locationRefsNode |> ignore
    universeNode.Expand()
    rootNode.Nodes.Add universeNode         |> ignore
   

    treeSplit.Panel1.Controls.Add tree 

    // TREE CONTEXT INSPECTOR
    let contextGrid = new DataGridView(Dock = DockStyle.Fill)
    contextGrid.Dock <- DockStyle.Fill
    treeSplit.Panel2.Controls.Add contextGrid 

    contextGrid.SelectionMode <- DataGridViewSelectionMode.FullRowSelect
    
    contextGrid.ContextMenu <- new ContextMenu()
    
    contextGrid.MouseClick
    |> Event.add (fun args -> if args.Button = MouseButtons.Right then contextGrid.ContextMenu.Show(contextGrid, args.Location) )

    contextGrid.SelectionChanged
    |> Event.add(fun _ -> 
        for item in contextGrid.ContextMenu.MenuItems do item.Dispose()
        contextGrid.ContextMenu.MenuItems.Clear()
        if contextGrid.SelectedRows.Count = 1 then
            match contextGrid.SelectedRows.[0].Cells.[1].Value.ToString() with
            | "function" -> 
                let addr =contextGrid.SelectedRows.[0].Cells.[2].Value.ToString()
                let mnu = new MenuItem("Goto function at " + addr)
                mnu.Click |> Event.add(fun _ -> gotoInstruction addr)
                printfn "adding menu item"
                contextGrid.ContextMenu.MenuItems.Add(mnu) |> ignore
            | _ -> ()
        
            
    )

    let showObjectContext (gameObject:JObject) =
        status.Text <- "object context hit " + gameObject.ToString()
        printfn "context %s" (gameObject.ToString())
        let contextMenu = new ContextMenu()
        let data = ResizeArray<ObjectContext>()
        for kvp in gameObject do
            printfn "kvp %s : %A" kvp.Key kvp.Value
            if kvp.Key = "id" then data.Add {kind = "ID"; key = kvp.Key; data = kvp.Value.ToString() } 
            else
            let ty = kvp.Value.["type"].Value<string>()
            printfn "object is type %s" ty
            match ty with
            | "array" -> data.Add {kind = ty; key = kvp.Key; data = "TODO!" }
            | "function" -> data.Add {kind = ty; key = kvp.Key; data = kvp.Value.["address"].Value<int>() |> toHex }
            | "go" -> data.Add {kind = ty; key = kvp.Key; data = kvp.Value.["id"].Value<int>().ToString() }
            | "int" -> data.Add {kind = ty; key = kvp.Key; data = kvp.Value.["value"].Value<int>().ToString() }
            | "string" -> data.Add {kind = ty; key = kvp.Key; data = kvp.Value.["value"].Value<string>() }
            | _ -> data.Add {kind = ty; key = kvp.Key; data = kvp.Value.ToString() }

        contextGrid.DataSource <- System.ComponentModel.BindingList(data)        
        ()

    // TREE / CONEXT EVENT HANDLING
    
    tree.AfterSelect
    |> Event.add(fun args -> 
        if args.Node.Parent <> null && args.Node.Parent.Text = "Game Objects" then
            showObjectContext (args.Node.Tag :?> JObject)    
        )

    // TABS
    let tabs = new TabControl(Dock = DockStyle.Fill)    
    tabs.TabPages.Add "Client Eumulation"
    tabs.TabPages.Add "Messaging"
    tabs.TabPages.Add "Debug Output"

    // MESSAGE TAB
    let jsonText = new RichTextBox()
    jsonText.Multiline <- true
    jsonText.Dock <- DockStyle.Fill
    tabs.TabPages.[1].Controls.Add jsonText
    
    // DEBUG OUTPUT TAB
    let debugText = new RichTextBox()
    debugText.Multiline <- true
    debugText.Dock <- DockStyle.Fill
    tabs.TabPages.[2].Controls.Add debugText


    // RIGHT MAIN VERTICAL SPLIT
    rightSplit.Panel1.Controls.Add treeSplit
    rightSplit.Panel2.Controls.Add tabs


    f.Controls.Add(mainSplit)

    //STATUS BAR
    status.Dock <- DockStyle.Bottom
    status.Text <- "DREY VM CONNECTED!"
    f.Controls.Add(status)

    f.Menu <- new MainMenu()
    let serverMenu = new MenuItem("Server")

    f.Menu.MenuItems.Add serverMenu |> ignore
    
    let connectItem = new MenuItem("Connect")
    let getProgramItem = new MenuItem("Get Program")

    serverMenu.MenuItems.Add connectItem    |> ignore
    serverMenu.MenuItems.Add getProgramItem |> ignore
        
    connectItem.Click
    |> Event.add(fun args -> inbox.Post(ReConnect))

    getProgramItem.Click
    |> Event.add(fun args -> inbox.Post GetProgram)

    f.Menu.MenuItems.Add serverMenu |> ignore
    
    let debugMenu = new MenuItem("Debug")
    let gotoItem = new MenuItem("Goto Address")
    let stepOverItem = new MenuItem("Step Over")
    stepOverItem.Shortcut <- Shortcut.F10
    let stepIntoItem = new MenuItem("Step Into")
    stepIntoItem.Shortcut <- Shortcut.F11
    let stepOutItem = new MenuItem("Step Out")
    stepOutItem.Shortcut <- Shortcut.ShiftF10
    gotoItem.Shortcut <- Shortcut.CtrlG

    stepIntoItem.Click 
    |> Event.add(fun args ->
        inbox.Post(Step)
    )

    gotoItem.Click
    |> Event.add(fun args ->
             let res = Microsoft.VisualBasic.Interaction.InputBox("Hex Address", "Goto", "").ToUpper()
             gotoInstruction res
        
        )
    
    debugMenu.MenuItems.Add stepOverItem |> ignore
    debugMenu.MenuItems.Add stepIntoItem |> ignore
    debugMenu.MenuItems.Add stepOutItem  |> ignore
    debugMenu.MenuItems.Add gotoItem     |> ignore


    f.Menu.MenuItems.Add serverMenu |> ignore
    f.Menu.MenuItems.Add debugMenu |> ignore
    
    disassemblyGrid.KeyUp
    |> Event.add(fun args ->
        if args.KeyCode = Keys.F9 then
            for r in disassemblyGrid.SelectedRows do
              let c = r.Cells.[0].Value.ToString()
              if r.DefaultCellStyle.BackColor <> System.Drawing.Color.Red then
                inbox.Post(SetBreakpoint (fromHex c))
              else
                inbox.Post(ClearBreakpoint (fromHex c))
        
        
        )
    

    { disassGrid = disassemblyGrid
      tabs = tabs
      mb = inbox
      jsonArea = jsonText    
      gotoInstruction = gotoInstruction
      objectsNode = gameObjectsNode
      machinesNode = machinesNode
      program = Program.Blank }



use context = new Context()
use client  = dealer context

let createDebugMsg = debugMsg()


let mb = new  MailboxProcessor<DebugMessageType>(fun inbox -> 
    Socket.setOption client (ZMQ.IDENTITY, (encode "__DEBUG__"))
    "tcp://localhost:5560"|> connect client 
    [|0x1uy|] |> send client
    let arr = ref [|[|0uy|]|]
    let rec loop state = async {
        let! msg = inbox.TryReceive(10)
        match msg with
        | Some (InstallNewMessageHandler h) ->
            return! loop { state with msgHandler = h }
        | Some (InstallNewUI ui) ->
            return! loop { state with ui = ui }
        | Some (ReConnect) ->
            Socket.setOption client (ZMQ.IDENTITY, (encode "__DEBUG__"))
            "tcp://localhost:5560"|> connect client 
            [|0x1uy|] |> send client
            return! loop state
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
                    state.msgHandler  msgType json state.ui
                    return! loop state                              
                else
                    //send heartbeat
                    if DateTime.Now - state.lastHeart > TimeSpan.FromMilliseconds 100. then
                        [|0x2uy|] |> send client
                        return! loop { state with lastHeart = DateTime.Now }
                    else
                        return! loop state
            with
            | ex -> printf "%A" (ex.ToString())
                    
    }
    loop { lastHeart = DateTime.Now; msgHandler = handleMessage; ui = UI.Blank }
)


let ui = createUi mb
f.Show()

mb.Post(InstallNewUI ui)

mb.Start()


mb.Post(InstallNewMessageHandler handleMessage)

// mb.Post(ReConnect)



f.Show()
f.Show()
