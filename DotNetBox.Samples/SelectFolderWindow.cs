using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetBox.Samples
{

    public partial class SelectFolderWindow : Form
    {

        private string folderPath;
        public string FolderPath { get { return folderPath; } }

        private bool dialogResult = false;

        public SelectFolderWindow()
        {

            InitializeComponent();

            AcceptButton = selectButton;

        }

        private async void SelectFolderWindow_Load(object sender, EventArgs e)
        {

            TreeNode node = new TreeNode("Root");

            node.Nodes.AddRange(await GetNodes(""));

            treeView.Nodes.Add(node);

        }

        private async Task<TreeNode[]> GetNodes(string path)
        {

            Metadata[] entries = (await MainForm.Client.Files.ListFolder(path)).Entries;

            List<TreeNode> nodes = new List<TreeNode>();

            foreach (Metadata entry in entries)
            {
                
                if (entry.IsFolder)
                {

                    TreeNode node = new TreeNode();

                    node.Name = entry.Name;
                    node.Text = entry.Name;
                    node.Tag = entry;
                    node.Nodes.Add("");

                    nodes.Add(node);

                }

            }

            return nodes.ToArray();

        }

        private async void treeView_AfterExpand(object sender, TreeViewEventArgs e)
        {

            e.Node.Nodes.Clear();

            e.Node.Nodes.AddRange(await GetNodes((e.Node.Tag != null ? (e.Node.Tag as Metadata).Path : "")));

        }

        private async void selectButton_Click(object sender, EventArgs e)
        {

            if (string.IsNullOrEmpty(textBox.Text) || await MainForm.Client.Files.FolderExists(textBox.Text))
            {

                folderPath = textBox.Text;
                dialogResult = true;

                Close();

            }
            else
            {

                MessageBox.Show("That folder does not exist!");

            }

        }

        public new bool ShowDialog()
        {

            base.ShowDialog();

            return dialogResult;

        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {

            textBox.Text = treeView.SelectedNode.Tag != null ? (treeView.SelectedNode.Tag as Metadata).Path : "";

        }

    }

}
