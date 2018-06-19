using drey_remote_debug.Docks;
using DreyZ;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace drey_remote_debug
{
    public interface IDockID
    {
        int ID { get; }
    }

    public static class DebuggerUI
    {
        public static Form1 MainForm { get; set; }
        public static List<DockContent> DockContents = new List<DockContent>();
        public static ZMQMailbox ZMB = new ZMQMailbox();
        public static ZMQMailbox ZMB2 = new ZMQMailbox();
        public static IList<ContextContent> ContextContentDataSource
        {
            get
            {
                var ret = new List<ContextContent>();
                foreach (var d in DockContents)
                {
                    if (d is ContextContent c)
                    {
                        ret.Add(c);
                    }
                }
                return ret;
            }
        }

        public static void UpdateContextDataSources()
        {
            foreach (var d in DockContents)
            {
                if (d is ContextContent c)
                {
                    c.RefreshContextDataSource();
                }
            }

        }
        private static T GetDockById<T>(int id) where T : DockContent, IDockID
        {
            foreach (var d in DockContents)
            {

                if (d.GetType() == typeof(T))
                {
                    T item = (T)d;
                    if (item.ID == id)
                    {
                        return item;
                    }
                }
            }
            return default(T);
        }
        public static void OnGotoAddress(int address, int disassId)
        {
            var c = GetDockById<DisassemblyGridContent>(disassId);
            if (c == null)
            {
                return;
            }

            c.GotoAddress(address);
        }
        public static void OnObjectSelect(object obj, int contextId)
        {
            var c = GetDockById<ContextContent>(contextId);
            if (c == null)
            {
                return;
            }
            if (obj != null)
            {
                c.ShowObject(obj);
            }
        }

        public static void UpdateProgram(DreyProgram program)
        {
            foreach (var dock in DockContents)
            {
                if (dock is DisassemblyGridContent d)
                {
                    dock.BeginInvoke(new Action(() => d.UpdateData(program.ByteCode)));
                }
            }

            var main = GetDockById<DisassemblyGridContent>(0);

        }

        public static void UpdateAnnounce(GameState state)
        {
            foreach (var dock in DockContents)
            {
                if (dock is MachineTreeContent d)
                {
                    dock.BeginInvoke(new Action(() => { d.UpdateData(state); d.RefreshSelected(); }));
                }
            }
            var main = GetDockById<DisassemblyGridContent>(0);
            main.BeginInvoke(new Action(() => main.UpdateLatestStep(state)));
        }


        internal static void Breakpoint(int address, bool set)
        {
            foreach (var dock in DockContents)
            {
                if (dock is DisassemblyGridContent d)
                {
                    dock.BeginInvoke(new Action(() => d.Breakpoint(address,set)));
                }
            }            
        }

        internal static void DebugMessage(string message)
        {
            foreach (var dock in DockContents)
            {
                if (dock is DebugOutputContent d)
                {
                    dock.BeginInvoke(new Action(() => d.AppendText(message)));
                }
            }
        }
        internal static ContextMenuStrip PopulateContextMenuItems(ContextMenuStrip ctx, object obj, bool clear = true)
        {
            if (ctx == null)
            {
                ctx = new ContextMenuStrip();
                ctx.ItemClicked += ContextMenuStrip_ItemClicked;
            }
            else if (clear)
            {
                ctx.Items.Clear();
            }

            if (obj is KeyValuePair<string, ObjectType> kvp)
            {
                obj = kvp.Value;
            }

            if (obj is KeyValuePair<string, GameObjectReference> kvp4)
            {
                obj = ZMB.GameState.GameObjectLookup[kvp4.Value.ID];
            }

            if(obj is GameObjectReference gor)
            {
                obj = ZMB.GameState.GameObjectLookup[gor.ID];
            }

            if (obj is KeyValuePair<int, GameObject> kvp2)
            {
                obj = kvp2.Value;
            }

         

            if (obj is ContextContent.ContextWrapper cw)
            {
                obj = cw.Link;
            }


            if (obj is DreyZ.Array arr)
            {
                obj = arr.Values;
            }
            if (obj is Scope
               || obj is List<Scope>
               || obj is PendingChoice
               || obj is GameObject
               || obj is IntValue
               || obj is StringValue
               || obj is List<ObjectType>
               || obj is Function)
            {
                string name = obj.GetType().Name;
                if(obj is List<ObjectType>)
                {
                    name = "array";
                }
                var items = CreateContextMenuItems($"Show {name} in",
                   new Action<ContextContent>(d =>
                   {
                       d.ShowObject(obj);
                   }));
                ctx.Items.AddRange(items);
            }


            if (obj is Scope s && s.Return_Address != 0)
            {
                var items = CreateContextMenuItems("Follow return address in",
                   new Action<DisassemblyGridContent>(d =>
                   {
                       d.GotoAddress(s.Return_Address);
                   }));
                ctx.Items.AddRange(items);
            }

            if (obj is ContextContent.ScopeWrapper sw)
            {
                if (sw.ReturnAddress != 0)
                {
                    ctx.Items.AddRange(
                        CreateContextMenuItems("Follow return address in",
                        new Action<DisassemblyGridContent>(d =>
                        {
                            d.GotoAddress(sw.ReturnAddress);
                        })));
                }
                ctx = PopulateContextMenuItems(ctx, sw.Type, false);
            }

            if (obj is Function f)
            {
                var items = CreateContextMenuItems("Follow function in",
                   new Action<DisassemblyGridContent>(d =>
                   {
                       d.GotoAddress(f.Address);
                   }));
                ctx.Items.AddRange(items);
            }
            return ctx;

        }

        public static void ContextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Tag is Action)
            {
                ((Action)e.ClickedItem.Tag)();
            }
        }

        internal static ToolStripMenuItem[] CreateContextMenuItems<T>(string pretext, Action<T> action) where T : DockContent, new()
        {
            var ret = new List<ToolStripMenuItem>();
            foreach (var dock in DockContents)
            {
                if (dock.GetType() == typeof(T))
                {
                    ret.Add(new ToolStripMenuItem($"{pretext} {dock.Text}")
                    {
                        Tag = new Action(() => action((T)dock))
                    });
                }
            }
            string name = "unknown";
            if (typeof(T) == typeof(DisassemblyGridContent))
            {
                name = "disassembly grid";
            }
            else if (typeof(T) == typeof(ContextContent))
            {
                name = "context view";
            }

            ToolStripMenuItem item = new ToolStripMenuItem($"{pretext} new {name}")
            {
                Tag = new Action(() =>
                {
                    var d = new T();
                    DockContents.Add(d);
                    UpdateContextDataSources();
                    d.Show(MainForm.dockPanel, DockState.Float);
                    action(d);
                })
            };
            ret.Add(item);
            return ret.ToArray();
        }

        
    }


    public partial class Form1 : Form
    {
        public DockPanel dockPanel;



        public Form1()
        {
            InitializeComponent();
            DebuggerUI.MainForm = this;
            this.IsMdiContainer = true;
            this.Width = 1500;

            var menu = new MainMenu();
            this.Menu = menu;
            var server = new MenuItem("Server");
            menu.MenuItems.Add(server);
            var connect = new MenuItem("Connect");
            connect.Click += Connect_Click;
            var getProgram = new MenuItem("Get Program");
            server.MenuItems.Add(connect);
            server.MenuItems.Add(getProgram);
            getProgram.Click += GetProgram_Click;

            var view = new MenuItem("View");
            menu.MenuItems.Add(view);

            var viewOutput = new MenuItem("Debug Output");
            view.MenuItems.Add(viewOutput);
            viewOutput.Click += ViewOutput_Click;


            var viewStrings = new MenuItem("String Table");
            view.MenuItems.Add(viewStrings);
            viewStrings.Click += ViewStrings_Click;

            var viewMachine = new MenuItem("Machine Tree");
            view.MenuItems.Add(viewMachine);
            viewMachine.Click += ViewMachine_Click;

            var viewContext = new MenuItem("Context");
            view.MenuItems.Add(viewContext);
            viewContext.Click += ViewContext_Click;

            var debug = new MenuItem("Debug");
            menu.MenuItems.Add(debug);
            var run = new MenuItem("Run");
            run.Shortcut = Shortcut.F5;
            debug.MenuItems.Add(run);
            run.Click += Run_Click;

            var stepInto = new MenuItem("Step Into");
            stepInto.Shortcut = Shortcut.F11;
            debug.MenuItems.Add(stepInto);
            stepInto.Click += StepInto_Click;

            var stepOver = new MenuItem("Step Over");
            stepOver.Shortcut = Shortcut.F10;
            debug.MenuItems.Add(stepOver);
            stepOver.Click += StepOver_Click;

            var stepOut = new MenuItem("Step Out");
            stepOut .Shortcut = Shortcut.ShiftF11;
            debug.MenuItems.Add(stepOut);
            stepOut.Click += StepOut_Click;

            this.dockPanel = new WeifenLuo.WinFormsUI.Docking.DockPanel
            {
                Dock = System.Windows.Forms.DockStyle.Fill
            };
            this.Controls.Add(this.dockPanel);
            var grid = new DisassemblyGridContent();
            grid.Show(this.dockPanel, DockState.DockLeft);
            var context = new ContextContent();
            context.Show(this.dockPanel, DockState.Document);
            var tree = new MachineTreeContent();
            tree.Show(this.dockPanel, DockState.DockRight);

            var output = new DebugOutputContent();
            output.Show(this.dockPanel, DockState.Document);
            //this.WindowState = FormWindowState.Maximized;
            this.dockPanel.DockLeftPortion = 0.3;
            DebuggerUI.DockContents.Add(grid);
            DebuggerUI.DockContents.Add(context);
            DebuggerUI.DockContents.Add(output);
            DebuggerUI.DockContents.Add(tree);
            DebuggerUI.UpdateContextDataSources();
            DebuggerUI.ZMB.DataArrived += _mb_DataArrived;
            DebuggerUI.ZMB.SetIdentity("__DEBUG__");
            DebuggerUI.ZMB.Connect("tcp://localhost:5560");
            Thread.Sleep(500);
            DebuggerUI.ZMB.GetProgramData();

        }

        private void ViewOutput_Click(object sender, EventArgs e)
        {
            var dock = new DebugOutputContent();
            DebuggerUI.DockContents.Add(dock);
            DebuggerUI.UpdateContextDataSources();
            dock.Show(this.dockPanel, DockState.Float);
        }

        private void StepOut_Click(object sender, EventArgs e)
        {
            DebuggerUI.ZMB.StepOut();
        }

        private void StepOver_Click(object sender, EventArgs e)
        {
            DebuggerUI.ZMB.StepOver();
        }

        private void Run_Click(object sender, EventArgs e)
        {
            DebuggerUI.ZMB.Run();
        }

        private void StepInto_Click(object sender, EventArgs e)
        {
            DebuggerUI.ZMB.StepInto();
        }

        private void ViewContext_Click(object sender, EventArgs e)
        {
            var dock = new ContextContent();
            DebuggerUI.DockContents.Add(dock);
            DebuggerUI.UpdateContextDataSources();
            dock.Show(this.dockPanel, DockState.Float);

        }

        private void ViewMachine_Click(object sender, EventArgs e)
        {
            var dock = new MachineTreeContent();
            dock.Show(this.dockPanel, DockState.Float);
            DebuggerUI.DockContents.Add(dock);
        }

        private void ViewStrings_Click(object sender, EventArgs e)
        {
            var dock = new StringTableGridContent();
            dock.Show(this.dockPanel, DockState.Float);
            DebuggerUI.DockContents.Add(dock);
        }

        private void ViewDisass_Click(object sender, EventArgs e)
        {
            var dock = new DisassemblyGridContent();
            dock.Show(this.dockPanel, DockState.Float);
            DebuggerUI.DockContents.Add(dock);
        }

        private void Connect_Click(object sender, EventArgs e)
        {
            DebuggerUI.ZMB2.SetIdentity("__DEBUG__2");
            DebuggerUI.ZMB2.Connect("tcp://localhost:5560");
            DebuggerUI.ZMB2.DataArrived += ZMB_DataArrived;
        }

        private void ZMB_DataArrived(ZMQMailbox.DreyEventArgs args)
        {
            
        }

        private void GetProgram_Click(object sender, EventArgs e)
        {
            DebuggerUI.ZMB.GetProgramData();
        }

        private void _mb_DataArrived(ZMQMailbox.DreyEventArgs args)
        {
            if (args is ZMQMailbox.GetProgramEventArgs gp)
            {
                DebuggerUI.UpdateProgram(gp.Program);
            }
            else if (args is ZMQMailbox.AnnounceEventArgs aa)
            {
                DebuggerUI.UpdateAnnounce(aa.State);
            }
            else if(args is ZMQMailbox.BreakPointEventArgs bp)
            {
                DebuggerUI.Breakpoint(bp.Address, bp.Set);
            }
            else if (args is ZMQMailbox.DebugMessageEventArgs dm)
            {
                DebuggerUI.DebugMessage(dm.Message);
            }
        }
    }




}
