using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace MCHI
{
    public partial class MainForm : Form
    {
        private HIO2Server server;
        private JHIClient jhiClient;
        private JORServer jorServer;
        private Timer timer = new Timer();

        public MainForm()
        {
            InitializeComponent();

            server = new HIO2Server();

            timer.Tick += OnTimerTick;
            timer.Interval = 16;
            timer.Start();
        }

        private HIO2ServerClient currentClient = null;

        private void SetCurrentClient(HIO2ServerClient client)
        {
            if (client == currentClient)
                return;

            currentClient = client;
            jhiClient = currentClient != null ? new JHIClient(currentClient) : null;
            jorServer = currentClient != null ? new JORServer(jhiClient) : null;
            if (jorServer != null)
                jorServer.NodeUpdated += NodeUpdated;
            hexBox1.ByteProvider = currentClient != null ? new DynamicFileByteProvider(currentClient.file.CreateViewStream()) : null;
            SyncPanelControls(null);
        }

        private void OnTimerTick(Object sender, EventArgs args)
        {
            server.Update();
            SetCurrentClient(server.Client);

            hexBox1.Invalidate();
            if (currentClient.IsConnected() && jhiClient != null)
            {
                jhiClient.Update();
                StatusLabel.Text = String.Format("Connected - Incoming Buffer {0:X8}", jhiClient.GetUnprocessedDataSize());
            }
            else
            {
                StatusLabel.Text = "Not Connected";
            }

            if (jorServer != null)
                jorServer.Update();
        }

        private TreeNode FindTreeNodeChild(TreeNode treeNode, JORNode childJorNode)
        {
            foreach (TreeNode childTreeNode in treeNode.Nodes)
                if (childTreeNode.Tag == childJorNode)
                    return childTreeNode;
            return null;
        }

        private TreeNode FindTreeNodeForJORNode(JORNode jorNode, TreeNode treeNode)
        {
            if (treeNode.Tag == jorNode)
                return treeNode;
            foreach (TreeNode childTreeNode in treeNode.Nodes)
            {
                var goodNode = FindTreeNodeForJORNode(jorNode, childTreeNode);
                if (goodNode != null)
                    return goodNode;
            }
            return null;
        }

        private void SyncTreeNode(TreeNode treeNode, JORNode jorNode)
        {
            if (treeNode.Tag != jorNode)
                treeNode.Tag = jorNode;
            treeNode.Text = jorNode.Name;

            var foundNodes = new List<TreeNode>();
            foreach (var childJorNode in jorNode.Children)
            {
                TreeNode childTreeNode = FindTreeNodeChild(treeNode, childJorNode);
                if (childTreeNode == null)
                {
                    childTreeNode = new TreeNode();
                    treeNode.Nodes.Add(childTreeNode);
                }
                SyncTreeNode(childTreeNode, childJorNode);
                foundNodes.Add(childTreeNode);
            }

            // remove extraneous tree nodes
            for (int i = treeNode.Nodes.Count - 1; i >= 0; i--)
                if (!foundNodes.Contains(treeNode.Nodes[i]))
                    treeNode.Nodes.RemoveAt(i);
        }

        private void NodeUpdated(JORNode jorNode)
        {
            if (ControlPanel.Controls.Count > 0)
            {
                JORPanel panel = ControlPanel.Controls[0] as JORPanel;
                if (jorNode == panel.Node)
                    SyncPanelControls(jorNode);
            }

            if (this.treeView1.Nodes.Count > 0)
            {
                var treeNode = FindTreeNodeForJORNode(jorNode, this.treeView1.Nodes[0]);
                if (treeNode != null)
                    SyncTreeNode(treeNode, jorNode);
            }
            else if (jorNode == this.jorServer.Root.TreeRoot)
            {
                SyncTree();
            }
        }

        private void SyncTree()
        {
            if (this.treeView1.Nodes.Count == 0)
                this.treeView1.Nodes.Add(new TreeNode());

            var treeNode = this.treeView1.Nodes[0];
            SyncTreeNode(treeNode, this.jorServer.Root.TreeRoot);
        }

        private void SyncPanelControls(JORNode node)
        {
            GroupBox panel = ControlPanel;

            if (panel.Controls.Count > 0)
            {
                var jorPanel = panel.Controls[0] as JORPanel;
                jorPanel.Destroy();
            }

            panel.Controls.Clear();
            if (jorServer != null && node != null)
            {
                if (node.Status == JORNodeStatus.GenRequestSent)
                {
                    // If we haven't received a GenRequest response, it might have gotten stuck. Poke it again.
                    jorServer.SendGenObjectInfo(node);
                }

                var jorPanel = new JORPanel(jorServer, node);
                jorPanel.Dock = DockStyle.Fill;
                panel.Controls.Add(jorPanel);
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            JORNode node = e.Node.Tag as JORNode;
            SyncPanelControls(node);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SyncTree();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (jorServer != null)
                jorServer.SendGetRootObjectRef();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            JORNode jorNode = treeView1.SelectedNode?.Tag as JORNode;
            if (jorServer != null && jorNode != null)
                jorServer.SendGenObjectInfo(jorNode);
        }
    }
}
