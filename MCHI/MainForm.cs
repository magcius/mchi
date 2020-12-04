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

        private void OnTimerTick(Object sender, EventArgs args)
        {
            UpdateClientCountLabel();
            if (jhiClient != null)
                jhiClient.Update();
            if (jorServer != null)
                jorServer.Update();
        }

        private TreeNode MakeTreeNode(JORNode jorNode)
        {
            var treeNode = new TreeNode(jorNode.Name);
            treeNode.Tag = jorNode;
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

        private void JHIWrite(HIO2ServerClient client, int offs, int size)
        {
            hexBox1.Invalidate();
        }

        private HIO2ServerClient currentClient = null;

        private void SetCurrentClient(HIO2ServerClient client)
        {
            if (client == currentClient)
                return;

            if (currentClient != null)
                currentClient.Write -= JHIWrite;
            currentClient = client;
            if (currentClient != null)
                currentClient.Write += JHIWrite;

            jhiClient = currentClient != null ? new JHIClient(currentClient) : null;
            jorServer = currentClient != null ? new JORServer(jhiClient) : null;
            hexBox1.ByteProvider = currentClient != null ? new DynamicByteProvider(currentClient.buf) : null;
            SyncTree();
            SyncPanelControls(null);
        }

        private void UpdateClientCountLabel()
        {
            ClientCountLabel.Text = String.Format("{0} connected clients...", server.clients.Count());

            if (server.clients.Count > 0)
            {
                HIO2ServerClient client = server.clients.Values.First();
                SetCurrentClient(client);
            }
            else
            {
                SetCurrentClient(null);
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
    }
}
