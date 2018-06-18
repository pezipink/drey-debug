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

        public DisassemblyGridContent()
        {
            _id = maxId++;
            Text = $"Disassembly {_id}";
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.ReadOnly = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.Controls.Add(_grid);
            if (DebuggerUI.ZMB.GameState?.Program != null)
            {
                UpdateData(DebuggerUI.ZMB.GameState.Program.ByteCode);
            }
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

        public void UpdateLatestStep(GameState state)
        {
            var f = state.Fibers.First(x => x.ID == state.ExecDetails.fiberid);
            var e = f.Exec_Contexts.First(x => x.ID == state.ExecDetails.ecid);

            GotoAddress(e.PC+1);

            Text = $"Disassembly - Fiber {f.ID} - EC {e.ID}";
        }

        internal void GotoAddress(int address)
        {
            
            if (address == 0)
            {
                _grid.Rows[0].Cells[0].Selected = true;
            }
            else
            {
                try
                {
                    for (int i = 0; i < _grid.Rows.Count; i++)
                    {
                        if (((int)_grid.Rows[i].Cells[0].Value) == address)
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
        }
    }


}
