using DreyZ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace drey_remote_debug.Docks
{
    
    public class StringTableGridContent : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        
        private DataGridView _grid;

        public StringTableGridContent()
        {
            Text = "String Table";
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.Controls.Add(_grid);
            if (DebuggerUI.ZMB.GameState?.Program?.StringTable != null)
            {
                _grid.DataSource = DebuggerUI.ZMB.GameState.Program.StringTable.ToArray();
                _grid.Columns[0].HeaderText = "Id";
                _grid.Columns[1].HeaderText = "String";

            }
        }

    }
}
