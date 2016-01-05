using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetBox.Samples
{

    public partial class MainForm : Form
    {

        public static DropboxClient Client;

        public MainForm()
        {

            InitializeComponent();

        }

        private async void MainForm_Load(object sender, EventArgs e)
        {

            if (!string.IsNullOrEmpty(Properties.Settings.Default.AccessToken))
            {

                Client = new DropboxClient(Properties.Settings.Default.AccessToken);

                if (await Client.CheckConnection())
                {

                    toolStripProgressBar.Style = ProgressBarStyle.Marquee;
                    Enabled = false;

                    // get current account
                    FullAccount currentAccount = await Client.Users.GetCurrentAccount();

                    // display account name and id
                    connectionStatusLabel.Text = string.Format("Connected as {0} ({1})", currentAccount.Name.DisplayName, currentAccount.AccountId);

                    // get space usage
                    SpaceUsage usage = await Client.Users.GetSpaceUsage();
                    spaceUsageProgressBar.Value = (int)((float)usage.Used / (float)usage.Allocation.Allocated * 100f);
                    spaceUsageLabel.Text = string.Format("{0:0.00}% used ({1:n} of {2:n} GiB)", (float)usage.Used / (float)usage.Allocation.Allocated * 100f, usage.Used / 1073741824f, usage.Allocation.Allocated / 1073741824f);

                    // refresh tree view
                    await RefreshTreeView();

                    toolStripProgressBar.Style = ProgressBarStyle.Blocks;
                    Enabled = true;

                }
                else
                {

                    MessageBox.Show("Unable to connect to Dropbox. Please try connecting again.");

                }

            }
            else
            {

                Client = new DropboxClient("ziu5zipgww4tnww", "1z77a0ptmee7ez3");

            }

            Client.Files.DownloadFileProgressChanged += Files_DownloadFileProgressChanged;
            Client.Files.DownloadFileCompleted += Files_DownloadFileCompleted;
            Client.Files.UploadFileProgressChanged += Files_UploadFileProgressChanged;
            Client.Files.UploadFileCompleted += Files_UploadFileCompleted;

        }

        private async void connectToDropboxToolStripMenuItem_Click(object sender, EventArgs e)
        {

            // open authorization webpage
            Process.Start(Client.GetAuthorizeUrl(ResponseType.Code));

            // show input dialog
            string code = new ConnectWindow().ShowDialog();

            toolStripProgressBar.Style = ProgressBarStyle.Marquee;
            Enabled = false;

            // authorize entered code
            AuthorizeResponse response = await Client.AuthorizeCode(code);

            // save token
            Properties.Settings.Default.AccessToken = response.AccessToken;
            Properties.Settings.Default.Save();

            // get current account
            FullAccount currentAccount = await Client.Users.GetCurrentAccount();

            // display account name and id
            connectionStatusLabel.Text = string.Format("Connected as {0} ({1})", currentAccount.Name.DisplayName, currentAccount.AccountId);

            // refresh tree view
            await RefreshTreeView();

            toolStripProgressBar.Style = ProgressBarStyle.Blocks;
            Enabled = true;

        }

        private async Task RefreshTreeView()
        {
            
            treeView.Nodes.Clear();

            treeView.Nodes.AddRange(await GetNodes(""));

        }

        private async Task<TreeNode[]> GetNodes(string path)
        {

            Metadata[] entries = (await Client.Files.ListFolder(path)).Entries;
            
            TreeNode[] nodes = new TreeNode[entries.Length];

            for (int i = 0; i < entries.Length; i++)
            {

                TreeNode node = new TreeNode();

                if (entries[i].IsDeleted)
                {

                    node.ForeColor = Color.Red;

                }

                if (entries[i].IsFolder)
                {

                    node.Nodes.Add("");

                }

                node.Name = entries[i].Name;
                node.Text = entries[i].Name;
                node.Tag = entries[i];

                nodes[i] = node;

            }

            return nodes;

        }

        private void downloadButton_Click(object sender, EventArgs e)
        {

            Metadata item;

            if (treeView.SelectedNode != null && (item = treeView.SelectedNode.Tag as Metadata).IsFile)
            {

                SaveFileDialog sfd = new SaveFileDialog();

                sfd.FileName = Path.GetFileName(item.Path);
                sfd.Filter = Path.GetExtension(item.Path) + "|*" + Path.GetExtension(item.Path) + "|All Files|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {

                    Client.Files.Download(item.Path, sfd.FileName); // you can use await if you want, but you will not get cancellation/exception information

                }

            }
            else
            {

                MessageBox.Show("Please select a file to download.");

            }

        }

        private void Files_DownloadFileProgressChanged(DownloadFileProgressChangedEventArgs e)
        {

            toolStripProgressBar.Value = (int)e.Progress;

        }

        private async void treeView_AfterExpand(object sender, TreeViewEventArgs e)
        {

            toolStripProgressBar.Style = ProgressBarStyle.Marquee;
            Enabled = false;

            Metadata item = (Metadata)e.Node.Tag;

            if (item.IsFolder)
            {

                e.Node.Nodes.Clear();

                e.Node.Nodes.AddRange(await GetNodes(item.Path));

            }

            toolStripProgressBar.Style = ProgressBarStyle.Blocks;
            Enabled = true;

        }

        private async void button2_Click(object sender, EventArgs e)
        {

            Metadata entry = (Metadata)treeView.SelectedNode.Tag;
            
            if (MessageBox.Show("Are you sure you want to delete this " + (entry.IsFile ? "file" : "folder") + "?", "Are you sure?", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {

                await Client.Files.Delete(entry.Path);

                await RefreshTreeView();

            }

        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {

            Metadata item = (Metadata)e.Node.Tag;

            downloadButton.Enabled = item.IsFile;
            deleteButton.Enabled = item.IsFile || item.IsFolder;
            copyButton.Enabled = item.IsFile || item.IsFolder;
            previewButton.Enabled = Regex.IsMatch(item.Name, @"^.*\.(doc|docx|docm|ppt|pps|ppsx|ppsm|pptx|pptm|xls|xlsx|xlsm|rtf)$");
            thumbnailButton.Enabled = Regex.IsMatch(item.Name, @"^.*\.(jpg|jpeg|png|tiff|tif|gif|bmp)$");

        }
        
        private async void previewButton_Click(object sender, EventArgs e)
        {

            await Client.Files.GetPreview(((Metadata)treeView.SelectedNode.Tag).Path, "tmp.pdf");

            Process.Start("tmp.pdf");

        }

        private async void thumbnailButton_Click(object sender, EventArgs e)
        {

            await Client.Files.GetThumbnail(((Metadata)treeView.SelectedNode.Tag).Path, "tmp.jpg", ThumbnailFormat.Jpeg, ThumbnailSize.W1024H768);

            Process.Start("tmp.jpg");

        }

        private void toolStripSplitButton1_ButtonClick(object sender, EventArgs e)
        {

            Client.Files.Cancel();

        }

        private void uploadButton_Click(object sender, EventArgs e)
        {

            OpenFileDialog ofd = new OpenFileDialog();

            if (ofd.ShowDialog() == DialogResult.OK)
            {

                SelectFolderWindow sfw = new SelectFolderWindow();

                if (sfw.ShowDialog())
                {

                    Client.Files.Upload(ofd.FileName, sfw.FolderPath + "/" + Path.GetFileName(ofd.FileName), WriteMode.Add, true, false); // you can use await if you want, but you will not get cancellation/exception information

                }

            }

        }

        private void Files_UploadFileProgressChanged(UploadFileProgressChangedEventArgs e)
        {

            toolStripProgressBar.Value = (int)e.Progress;

        }

        private void Files_DownloadFileCompleted(DownloadFileCompletedEventArgs e)
        {

            MessageBox.Show("Done!");

        }

        private void Files_UploadFileCompleted(UploadFileCompletedEventArgs e)
        {

            MessageBox.Show("Done!");

        }

        private async void button3_Click(object sender, EventArgs e)
        {

            Metadata item;

            if (treeView.SelectedNode != null && (item = treeView.SelectedNode.Tag as Metadata).IsFile | item.IsFolder)
            {

                SelectFolderWindow sfw = new SelectFolderWindow();

                if (sfw.ShowDialog())
                {

                    await Client.Files.Copy(item.Path, sfw.FolderPath + "/" + item.Name);
    
                }

            }
            else
            {

                MessageBox.Show("Please select a file or folder to copy.");

            }

        }

    }

}
