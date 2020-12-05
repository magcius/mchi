using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            hexBox1.ByteProvider = currentClient != null ? new DynamicFileByteProvider(currentClient.file.CreateViewStream()) : null;
            SyncTree();
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

        private TreeNode MakeTreeNode(JORNode jorNode)
        {
            var treeNode = new TreeNode(jorNode.Name);
            treeNode.Tag = jorNode;
            treeNode.ToolTipText = String.Format("{0:X8}", jorNode.NodePtr);
            foreach (var child in jorNode.Children)
                treeNode.Nodes.Add(MakeTreeNode(child));
            return treeNode;
        }

        private void SyncTree()
        {
            treeView1.Nodes.Clear();

            if (jorServer != null)
            {
                TreeNode rootNode = MakeTreeNode(jorServer.Root.TreeRoot);
                treeView1.Nodes.Add(rootNode);
            }
        }

        private void SyncPanelControls(JORNode node)
        {
            GroupBox panel = ControlPanel;
            panel.Controls.Clear();
            if (jorServer != null && node != null)
            {
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
