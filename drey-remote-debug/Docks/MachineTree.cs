using DreyZ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace drey_remote_debug.Docks
{
    public class MachineTreeContent : WeifenLuo.WinFormsUI.Docking.DockContent
    {
        
        private TreeView _tree;
        private TreeNode _machineNode;
        private TreeNode _fibersNode;
        private TreeNode _universeNode;

        public MachineTreeContent()
        {
            
            Text = "Machine Tree";
            _tree = new TreeView();
            _tree.AfterSelect += _tree_AfterSelect;
            _machineNode = new TreeNode("Machine");
            _tree.Nodes.Add(_machineNode);

            _fibersNode = new TreeNode("Fibers");
            _machineNode.Nodes.Add(_fibersNode);

            _universeNode = new TreeNode("Universe");
            _machineNode.Nodes.Add(_universeNode);

            _tree.ExpandAll();
            _tree.Dock = DockStyle.Fill;
            this.Controls.Add(_tree);
            if (DebuggerUI.ZMB.GameState != null)
            {
                UpdateData(DebuggerUI.ZMB.GameState);

            }
        }

        private void _tree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if(e.Node.Tag != null)
            {
                e.Node.ContextMenuStrip = DebuggerUI.PopulateContextMenuItems(e.Node.ContextMenuStrip, e.Node.Tag);                
            }
            DebuggerUI.OnObjectSelect(e.Node.Tag, 0);
        }

       

        public void RefreshSelected()
        {
            var temp = _tree.SelectedNode;
            _tree.SelectedNode = null;
            _tree.SelectedNode = temp;
        }

        public void UpdateData(GameState state)
        {
            lock (state.SyncLock)
            {
                if (state.Fibers != null)
                {
                    UpdateFibers(state.Fibers);
                }

                if (state.Universe != null)
                {
                    UpdateUniverse(state.Universe);
                }

            }

        }

        private void UpdateUniverse(Universe universe)
        {

        }

        private void UpdateFibers(List<Fiber> fibers)
        {
            var existing = new Dictionary<Fiber, TreeNode>();
            var newFibers = fibers.ToDictionary(x => x);
            foreach (var node in _fibersNode.Nodes)
            {
                var tn = (TreeNode)node;
                existing.Add((Fiber)tn.Tag, tn);
            }

            foreach (var f in fibers)
            {
                if(f.Waiting_Data == null)
                {
                    f.Waiting_Data = new PendingChoice() { Choices = new Dictionary<string, string>(), Header = "" };
                }
                TreeNode n = null;
                TreeNode ecs = null;
                if (!existing.TryGetValue(f, out n))
                {
                    n = new TreeNode($"Fiber {f.ID}") { Tag = f };
                    ecs = new TreeNode("Execution Contexts") { Tag = f.Exec_Contexts };
                    var respones = new TreeNode("Awaiting Response") { Tag = f.Waiting_Data };
                    
                    n.Nodes.Add(ecs);
                    n.Nodes.Add(respones);
                    _fibersNode.Nodes.Add(n);
                    existing.Add(f, n);
                }
                else
                {
                    foreach (var c in n.Nodes)
                    {
                        if (((TreeNode)c).Tag is List<DreyZ.ExecutionContext>)
                        {
                            ecs = (TreeNode)c;
                            break;
                        }
                    }
                }

                UpdateExecutionContexts(ecs, f.Exec_Contexts);

            }

            var toRemove = new List<TreeNode>();
            foreach (TreeNode node in _fibersNode.Nodes)
            {
                if (!newFibers.ContainsKey((Fiber)node.Tag))
                {
                    toRemove.Add(node);
                }
            }
            foreach (var n in toRemove)
            {
                _fibersNode.Nodes.Remove(n);
            }

        }

        private void UpdateExecutionContexts(TreeNode ecs, List<DreyZ.ExecutionContext> execContexts)
        {
            var existing = new Dictionary<DreyZ.ExecutionContext, TreeNode>();
            var newEcs = execContexts.ToDictionary(x => x);
            foreach (var node in ecs.Nodes)
            {
                var tn = (TreeNode)node;
                existing.Add((DreyZ.ExecutionContext)tn.Tag, tn);
            }

            foreach (var ec in execContexts)
            {
                TreeNode n = null;
                TreeNode scopes = null;
                if (!existing.TryGetValue(ec, out n))
                {
                    n = new TreeNode($"Exec Context {ec.ID}") { Tag = ec };
                    scopes = new TreeNode("Scopes") { Tag = ec.Scopes };
                    var eval = new TreeNode("Eval Stack") { Tag = ec.EvalStack };
                    n.Nodes.Add(scopes);
                    n.Nodes.Add(eval);
                    ecs.Nodes.Add(n);
                    existing.Add(ec, n);
                }
                else
                {
                    foreach (var c in n.Nodes)
                    {
                        if (((TreeNode)c).Tag is List<DreyZ.Scope>)
                        {
                            scopes = (TreeNode)c;
                            break;
                        }
                    }
                }

                UpdateScopes(scopes, ec.Scopes);

            }

            var toRemove = new List<TreeNode>();
            foreach (TreeNode node in ecs.Nodes)
            {
                if (!newEcs.ContainsKey((DreyZ.ExecutionContext)node.Tag))
                {
                    toRemove.Add(node);
                }
            }
            foreach (var n in toRemove)
            {
                ecs.Nodes.Remove(n);
            }
        }

        private void UpdateScopes(TreeNode scopesNode, List<Scope> scopes)
        {
            var existing = new Dictionary<DreyZ.Scope, TreeNode>();
            var newScopes = scopes.ToDictionary(x => x);
            foreach (var node in scopesNode.Nodes)
            {
                var tn = (TreeNode)node;
                existing.Add((DreyZ.Scope)tn.Tag, tn);
            }

            foreach (var scope in scopes)
            {
                TreeNode n = null;

                if (!existing.TryGetValue(scope, out n))
                {
                    n = new TreeNode($"Scope") { Tag = scope };
        
                    scopesNode.Nodes.Add(n);
                    existing.Add(scope, n);

                }

            }

            var toRemove = new List<TreeNode>();
            foreach (TreeNode node in scopesNode.Nodes)
            {
                if (!newScopes.ContainsKey((Scope)node.Tag))
                {
                    toRemove.Add(node);
                }
            }
            foreach (var n in toRemove)
            {
                if(n.ContextMenuStrip != null)
                {
                    n.ContextMenuStrip.ItemClicked -= DebuggerUI.ContextMenuStrip_ItemClicked;
                    n.ContextMenuStrip.Dispose();
                }
                                
                scopesNode.Nodes.Remove(n);
            }
        }
        

    }

}
