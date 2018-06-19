using DreyZ;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace drey_remote_debug.Docks
{
    public class ContextContent : WeifenLuo.WinFormsUI.Docking.DockContent, IDockID
    {
        public class ContextWrapper
        {
            public string Kind { get; set; }
            public string Value { get; set; }
            public object Link;
        }

        public class ScopeWrapper
        {
            public int Depth { get; set; }
            public int ReturnAddress { get; set; }
            public string Key { get; set; }
            public ObjectType Type { get; set; }
        }

        private enum ContextType
        {
            Scope,
            Function,
            List,
            PendingChoices
        }
        public static int maxContextId = 0;
        public int _id = 0;
        private DataGridView _grid;

        private ContextType _currentType;
        private ToolStrip _toolBar;
        private ToolStripComboBox _childCombo;
        private ToolStripStatusLabel _statusBar;
        private ContextContent _childContext = null;

        public string FriendlyName { get => $"Context View {ID}"; }

        public int ID { get => _id; }

        public ContextContent()
        {
            _id = maxContextId++;
            if (_id == 0)
            {
                Text = "Context View 0";
                this.CloseButtonVisible = false;
            }
            else
            {
                Text = $"Context View {_id}";
            }
            _toolBar = new ToolStrip();
            _childCombo = new ToolStripComboBox();
            _statusBar = new ToolStripStatusLabel();
            _childCombo.ComboBox.DataSource = DebuggerUI.ContextContentDataSource;
            _childCombo.ComboBox.SelectedValueChanged += _childCombo_SelectedValueChanged;
            _childCombo.ComboBox.ValueMember = "FriendlyName";
            _toolBar.Items.Add(_childCombo);
            _toolBar.Dock = DockStyle.Top;
            _toolBar.Height = 10;
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _toolBar.Items.Add(_statusBar);
            _statusBar.Text = "TESTING";
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            this.Controls.Add(_toolBar);
            this.Controls.Add(_grid);
            _grid.ContextMenuStrip = new ContextMenuStrip();
            _grid.ContextMenuStrip.ItemClicked += ContextMenuStrip_ItemClicked;
            _grid.SelectionChanged += _grid_SelectionChanged;
            _grid.ColumnHeadersVisible = true;
            _grid.BringToFront();
            _toolBar.SendToBack();


        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _childCombo.SelectedItem = this;
            //for (int i = 0; i < ((IList<ContextContent>)_childCombo.ComboBox.DataSource).Count ; i++)
            //{
            //    var item = ((IList<ContextContent>)_childCombo.ComboBox.DataSource)[i];
            //    if (item == this)
            //    {
            //        _childCombo.ComboBox.SelectedIndex = i;
            //        return;

            //    }

            //}
        }

        private void _childCombo_SelectedValueChanged(object sender, EventArgs e)
        {
            if (_childCombo.SelectedItem == this)
            {
                _childContext = null;
            }
            else if (_childCombo.SelectedItem != this._childContext)
            {
                _childContext = (ContextContent)_childCombo.SelectedItem;
            }
        }

        private void ContextMenuStrip_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            if (e.ClickedItem.Tag is Action a)
            {
                a();
            }
        }

        private void _grid_SelectionChanged(object sender, EventArgs e)
        {
            _grid.ContextMenuStrip.Items.Clear();
            if (_grid.SelectedRows != null && _grid.SelectedRows.Count == 1)
            {
                DataGridViewRow row = _grid.SelectedRows[0];
                var obj = row.DataBoundItem;
                if (obj != null)
                {
                    _grid.ContextMenuStrip = DebuggerUI.PopulateContextMenuItems(_grid.ContextMenuStrip, obj);
                    HandleChildContext(obj);
                    if (_currentType == ContextType.PendingChoices)
                    {
                        if (_grid.Tag is Fiber f && obj is KeyValuePair<string, string> kvp)
                        {

                            ToolStripMenuItem item = new ToolStripMenuItem("Emulate this response")
                            {
                                Tag = new Action(() => DebuggerUI.ZMB.EmulateClientReponse(f.Waiting_Client, kvp.Key))
                            };
                            _grid.ContextMenuStrip.Items.Add(item);
                        }

                    }
                }
                


            }
        }

        private void HandleChildContext(object obj)
        {
            if (_childContext != null)
            {
                _childContext.ShowObject(obj);
            }
        }

        public void ShowObject(Object obj)
        {
            
            if (obj is KeyValuePair<string, ObjectType> kvp2)
            {
                obj = kvp2.Value;
            }

            if (obj is KeyValuePair<int, GameObject> kvp3)
            {
                obj = kvp3.Value;
            }

            if (obj is KeyValuePair<int, GameObjectReference> kvp4)
            {
                obj = DebuggerUI.ZMB.GameState.GameObjectLookup[kvp4.Value.ID];
            }

            if (obj is KeyValuePair<string, GameObjectReference> kvp5)
            {
                obj = DebuggerUI.ZMB.GameState.GameObjectLookup[kvp5.Value.ID];
            }

            if (obj is GameObjectReference gor)
            {
                obj = DebuggerUI.ZMB.GameState.GameObjectLookup[gor.ID];
            }

            if (obj is ScopeWrapper sw)
            {
                obj = sw.Type;
            }
            if (obj is DreyZ.Array arr)
            {
                obj = arr.Values;
            }

            _statusBar.Text = obj.GetType().Name;
            if (obj is Scope s)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = s.Locals.ToArray();
                _currentType = ContextType.Scope;
            }
            else if (obj is List<Scope> ss)
            {
                var items = new List<ScopeWrapper>();
                for (int i = ss.Count - 1; i >= 0; i--)
                {
                    foreach (var kvp in ss[i].Locals)
                    {
                        items.Add(new ScopeWrapper() { Depth = i, ReturnAddress = ss[i].Return_Address, Key = kvp.Key, Type = kvp.Value });
                    }
                }
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = items;
                _currentType = ContextType.Scope;
            }
            else if (obj is PendingChoice pc)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = pc.Choices.ToArray();
                // not nice. change design later.
                if (pc.Choices.Count > 0)
                {
                    _grid.Tag = DebuggerUI.ZMB.GameState.Fibers.First(x => x.ID == pc.FiberId);
                }
                else
                {
                    _grid.Tag = null;
                }
                _currentType = ContextType.PendingChoices;
            }
            else if (obj is Function f)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = new Function[] { f };
                _currentType = ContextType.Function;
            }
            else if (obj is StringValue sv)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = new StringValue[] { sv };
                _currentType = ContextType.Function;
            }
            else if (obj is IntValue iv)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = new IntValue[] { iv };
                _currentType = ContextType.Function;
            }
            else if (obj is GameObject go)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = go.Props.ToArray();
                _currentType = ContextType.Function;
                _statusBar.Text = $"GameObject {go.ID}";
            }
            else if (obj is GameObjectReference gor3)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = DebuggerUI.ZMB.GameState.GameObjectLookup[gor3.ID].Props.ToArray();
                _currentType = ContextType.Function;
                _statusBar.Text = $"GameObject {gor3.ID}";
            }
            else if (obj is ObjectType o)
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = new ObjectType[] { o };
                _currentType = ContextType.Function;
            }
            else if (obj is ContextWrapper cw)
            {
                this.ShowObject(cw.Link);
            }
            else if(obj is Dictionary<int, GameObject> god)  // game object dict
            {
                _grid.ContextMenuStrip.Items.Clear();
                _grid.DataSource = god.ToArray() ;
            }
            else if (obj is List<ObjectType>)
            {
                _grid.ContextMenuStrip.Items.Clear();
                List<ContextWrapper> items = new List<ContextWrapper>();
                foreach (var ot in (List<ObjectType>)obj)
                {
                    if (ot is StringValue sv2)
                    {
                        items.Add(new ContextWrapper() { Kind = "String", Value = sv2.Value, Link = sv2 });
                    }
                    else if (ot is IntValue iv2)
                    {
                        items.Add(new ContextWrapper() { Kind = "Int", Value = iv2.Value.ToString(), Link = iv2 });
                    }
                    else if (ot is DreyZ.Array arr2)
                    {
                        items.Add(new ContextWrapper() { Kind = "Array", Value = "", Link = arr2 });
                    }
                    else if (ot is DreyZ.GameObject go2)
                    {
                        items.Add(new ContextWrapper() { Kind = "GameObject", Value = go2.ID.ToString(), Link = go2 });
                    }
                    else if (ot is DreyZ.Function f2)
                    {
                        items.Add(new ContextWrapper() { Kind = "Function", Value = string.Format("{0:X}", f2.Address), Link = f2 });
                    }

                    else if (ot is DreyZ.GameObjectReference gor2)
                    {
                        items.Add(new ContextWrapper() { Kind = "GameObject", Value = gor2.ID.ToString(), Link = DebuggerUI.ZMB.GameState.GameObjectLookup[gor2.ID] });
                    }

                    else
                    {
                        items.Add(new ContextWrapper() { Kind = ot.GetType().Name, Value = "Not Implemented", Link = ot });
                    }
                }
                _grid.DataSource = items;
                _currentType = ContextType.List;

            }
            _grid_SelectionChanged(null, null);
        }


        internal void RefreshContextDataSource()
        {
            var selected = _childCombo.ComboBox.SelectedValue;
            _childCombo.ComboBox.DataSource = DebuggerUI.ContextContentDataSource;
            if (selected != null)
            {
                _childCombo.ComboBox.SelectedValue = selected;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            DebuggerUI.DockContents.Remove(this);
            DebuggerUI.UpdateContextDataSources();
        }
        
    }

}
