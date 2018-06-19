using DreyZ;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace drey_remote_debug.Docks
{
    public class DisassemblyGridContent : WeifenLuo.WinFormsUI.Docking.DockContent, IDockID
    {

        private BindingList<Instruction> _data;
        private DataGridView _grid;
        public static int maxId = 0;
        public int _id = 0;
        public int ID { get => _id; }
        public DataGridViewRow _executingRow = null;
        public DisassemblyGridContent()
        {
            _id = maxId++;
            Text = $"Disassembly {_id}";
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.Controls.Add(_grid);


            _grid.KeyUp += _grid_KeyUp; ;
            if (DebuggerUI.ZMB.GameState?.Program != null)
            {
                UpdateData(DebuggerUI.ZMB.GameState?.Program.ByteCode);
            }
        }

        private void _grid_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.F9)
            {
                e.Handled = true;
                if (_grid.SelectedRows.Count == 1)
                {
                    var address = Convert.ToInt32(_grid.SelectedRows[0].Cells[0].Value);
                    DebuggerUI.ZMB.Breakpoint(address, _grid.SelectedRows[0].DefaultCellStyle.BackColor.IsEmpty);
                }
            }
            else if (e.KeyCode == Keys.G && e.Control)
            {
                e.Handled = true;
                var str = Microsoft.VisualBasic.Interaction.InputBox("Enter Address (decimal or hex)");
                if (string.IsNullOrEmpty(str))
                {
                    return;
                }
                try
                {
                    if (str.ToLower().StartsWith("0x"))
                    {
                        GotoHexAddress(str.Substring(2).ToLower());
                    }
                    else
                    {
                        GotoAddress(Int32.Parse(str));
                    }
                }
                catch
                {

                    MessageBox.Show("Bad address.");
                }
            }
        }
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            UpdateStyles();
        }


        public void UpdateData(List<Instruction> instructions)
        {
            _data = new BindingList<Instruction>(instructions);
            _grid.DataSource = _data;
            _grid.Columns[0].HeaderText = "Address";
            _grid.Columns[1].HeaderText = "Hex";
            _grid.Columns[2].HeaderText = "Opcode";
            _grid.Columns[3].HeaderText = "Operand";
        }

        private void UpdateStyles()
        {
            if (DebuggerUI.ZMB.GameState.ExecDetails != null)
            {
                //var f = DebuggerUI.ZMB.GameState.Fibers.First(x => x.ID == DebuggerUI.ZMB.GameState.ExecDetails.fiberid);
                //var e = f.Exec_Contexts.First(x => x.ID == DebuggerUI.ZMB.GameState.ExecDetails.ecid);
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    DataGridViewRow row = _grid.Rows[i];
                    int address = Convert.ToInt32(row.Cells[0].Value);
                    //if (address == e.PC + 1)
                    //{
                    //    _executingRow = row;
                    //    _executingRow.DefaultCellStyle.BackColor = System.Drawing.Color.DarkGreen;
                    //    _executingRow.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.DarkGreen;
                    //} else

                    if (DebuggerUI.ZMB.GameState.Breakpoints.Contains(address))
                    {
                        row.DefaultCellStyle.BackColor = System.Drawing.Color.Red;
                        row.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.DarkRed;
                    }
                }
            }
        }

        public void UpdateLatestStep(GameState state)
        {
            try
            {
                var f = state.Fibers.First(x => x.ID == state.ExecDetails.fiberid);
                var e = f.Exec_Contexts.First(x => x.ID == state.ExecDetails.ecid);
                if (_executingRow != null)
                {
                    if (DebuggerUI.ZMB.GameState.Breakpoints.Contains(Convert.ToInt32(_executingRow.Cells[0].Value)))
                    {
                        _executingRow.DefaultCellStyle.BackColor = System.Drawing.Color.Red;
                        _executingRow.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.DarkRed;
                    }
                    else
                    {
                        _executingRow.DefaultCellStyle.BackColor = System.Drawing.Color.Empty;
                        _executingRow.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.Empty;
                    }
                }
                GotoAddress(e.PC + 1);
                _executingRow = _grid.SelectedRows[0];

                _executingRow.DefaultCellStyle.BackColor = System.Drawing.Color.DarkGreen;
                _executingRow.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.DarkGreen;

                Text = $"Disassembly - Fiber {f.ID} - EC {e.ID}";
            }
            catch (Exception)
            {
                //this happends when a fiber finishes execution since we see the last
                //executed instruction.  
                Console.WriteLine("WARNING, UNBALE TO LOCATE FIBER / EC");
                return;
            }
           

         
        }

        internal void GotoHexAddress(string address)
        {
            try
            {
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    object val = _grid.Rows[i].Cells[1].Value;

                    if (val != null && (((string)val).ToLower() == address))
                    {
                        _grid.CurrentCell = _grid.Rows[i].Cells[0];
                        _grid.ClearSelection();
                        _grid.Rows[i].Selected = true;
                        return;
                    }

                }
            }
            catch
            {
                //mystery
            }

        }
        internal void GotoAddress(int address)
        {

            try
            {
                for (int i = 0; i < _grid.Rows.Count; i++)
                {
                    object val = _grid.Rows[i].Cells[0].Value;
                    {
                        if (val != null && ((int)val == address))
                        {
                            _grid.CurrentCell = _grid.Rows[i].Cells[0];
                            _grid.ClearSelection();
                            _grid.Rows[i].Selected = true;
                            return;
                        }
                    }
                }
            }
            catch
            {
                //mystery
            }
        }


        internal void Breakpoint(int address, bool set)
        {
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                if (((int)_grid.Rows[i].Cells[0].Value) == address)
                {
                    _grid.Rows[i].DefaultCellStyle.BackColor = set ? System.Drawing.Color.Red : System.Drawing.Color.Empty;
                    _grid.Rows[i].DefaultCellStyle.SelectionBackColor = set ? System.Drawing.Color.DarkRed : System.Drawing.Color.Empty;
                    return;
                }
            }
        }
    }


}
