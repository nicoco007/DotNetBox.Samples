using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DotNetBox.Samples
{

    public partial class MainForm : Form
    {

        private const string appId = "ziu5zipgww4tnww";
        private const string appSecret = "1z77a0ptmee7ez3";

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
                    
                    EnableUI(false);

                    // get current account
                    FullAccount currentAccount = await Client.Users.GetCurrentAccount();

                    // display account name and id
                    connectionStatusLabel.Text = string.Format("Connected as {0} ({1})", currentAccount.Name.DisplayName, currentAccount.AccountId);

                    // get space usage
                    SpaceUsage usage = await Client.Users.GetSpaceUsage();
                    spaceUsageLabel.Text = string.Format("{0:0.00}% used ({1:n} of {2:n} GiB)", (float)usage.Used / (float)usage.Allocation.Allocated * 100f, usage.Used / 1073741824f, usage.Allocation.Allocated / 1073741824f);

                    // refresh tree view
                    await RefreshTreeView();
                    
                    EnableUI(true);

                }
                else
                {

                    MessageBox.Show("Unable to connect to Dropbox. Please try connecting again.");

                }

            }
            else
            {

                Client = new DropboxClient(appId, appSecret);

            }

            Client.Files.DownloadFileProgressChanged += Files_DownloadFileProgressChanged;
            Client.Files.UploadFileProgressChanged += Files_UploadFileProgressChanged;

        }

        private async void connectToDropboxToolStripMenuItem_Click(object sender, EventArgs e)
        {

            // create new Dropbox Client
            Client = new DropboxClient(appId, appSecret);

            // open authorization webpage
            Process.Start(Client.GetAuthorizeUrl(ResponseType.Code));

            // show input dialog
            string code = new ConnectWindow().ShowDialog();
            
            EnableUI(false);

            AuthorizeResponse response = null;

            // authorize entered code
            try
            {

                response = await Client.AuthorizeCode(code);

            }
            catch(InvalidGrantException ex)
            {

                MessageBox.Show("Invalid code. Please try again.", "Invalid code", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return;

            }

            // save token
            Properties.Settings.Default.AccessToken = response.AccessToken;
            Properties.Settings.Default.Save();

            // get current account
            FullAccount currentAccount = await Client.Users.GetCurrentAccount();

            // display account name and id
            connectionStatusLabel.Text = string.Format("Connected as {0} ({1})", currentAccount.Name.DisplayName, currentAccount.AccountId);

            // refresh tree view
            await RefreshTreeView();
            
            EnableUI(true);

        }

        private async Task RefreshTreeView()
        {
            
            ShowLoader(true);

            treeView.Nodes.Clear();

            treeView.Nodes.AddRange(await GetNodes(""));
            
            ShowLoader(false);

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

        private async void uploadButton_Click(object sender, EventArgs e)
        {

            OpenFileDialog ofd = new OpenFileDialog();

            if (ofd.ShowDialog() == DialogResult.OK)
            {

                SelectFolderWindow sfw = new SelectFolderWindow();

                if (sfw.ShowDialog())
                {

                    try
                    {

                        await Client.Files.Upload(ofd.FileName, sfw.FolderPath + "/" + Path.GetFileName(ofd.FileName), WriteMode.Add, false, false);

                    }
                    catch(OperationCanceledException) // check if user canceled operation
                    {

                        MessageBox.Show("Operation canceled!", "Operation canceled!", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    }
                    catch (DropboxException ex) // catch any other exception
                    {

                        MessageBox.Show(string.Format("An error of type {0} occured while trying to upload the file: {1}", ex.GetType(), ex.Message), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    }

                }

            }

        }

        private async void downloadButton_Click(object sender, EventArgs e)
        {

            Metadata item;

            if (treeView.SelectedNode != null && (item = treeView.SelectedNode.Tag as Metadata).IsFile)
            {

                SaveFileDialog sfd = new SaveFileDialog();

                sfd.FileName = Path.GetFileName(item.Path);
                sfd.Filter = Path.GetExtension(item.Path) + "|*" + Path.GetExtension(item.Path) + "|All Files|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {

                    try
                    {

                        await Client.Files.Download(item.Path, sfd.FileName);

                    }
                    catch (OperationCanceledException)
                    {

                        MessageBox.Show("Operation canceled!", "Operation canceled!", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    }
                    catch (DropboxException ex) // catch any other exception
                    {

                        MessageBox.Show(string.Format("An error of type {0} occured while trying to download the file: {1}", ex.GetType(), ex.Message), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    }

                    MessageBox.Show("Done!");

                }

            }
            else
            {

                MessageBox.Show("Please select a file to download.");

            }

        }

        private void Files_DownloadFileProgressChanged(DownloadFileProgressChangedEventArgs e)
        {

            progressBar.Value = (int)e.Progress;

        }

        private async void treeView_AfterExpand(object sender, TreeViewEventArgs e)
        {

            loadingImage.Visible = true;

            EnableUI(false);
            ShowLoader(true);

            Metadata item = (Metadata)e.Node.Tag;

            if (item.IsFolder)
            {

                e.Node.Nodes.Clear();

                e.Node.Nodes.AddRange(await GetNodes(item.Path));

            }

            ShowLoader(false);
            EnableUI(true);

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

        private void Files_UploadFileProgressChanged(UploadFileProgressChangedEventArgs e)
        {

            progressBar.Value = (int)e.Progress;

        }

        private async void button3_Click(object sender, EventArgs e)
        {

            Metadata item;

            if (treeView.SelectedNode != null && (item = treeView.SelectedNode.Tag as Metadata).IsFile | item.IsFolder)
            {

                SelectFolderWindow sfw = new SelectFolderWindow();

                if (sfw.ShowDialog())
                {
                    
                    try
                    {

                        await Client.Files.Copy(item.Path, sfw.FolderPath + "/" + item.Name);

                    }
                    catch (DropboxException ex) // catch any other exception
                    {

                        MessageBox.Show(string.Format("An error of type {0} occured while trying to download the file: {1}", ex.GetType(), ex.Message), "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    }

                }

            }
            else
            {

                MessageBox.Show("Please select a file or folder to copy.");

            }

        }

        private void EnableUI(bool enable)
        {

            treeView.Enabled = enable;
            uploadButton.Enabled = enable;
            downloadButton.Enabled = enable;
            deleteButton.Enabled = enable;
            copyButton.Enabled = enable;
            previewButton.Enabled = enable;
            thumbnailButton.Enabled = enable;
            cancelButton.Enabled = enable;
            progressBar.Style = enable ? ProgressBarStyle.Blocks : ProgressBarStyle.Marquee;
        }

        private void ShowLoader(bool show)
        {
            
            treeView.BackColor = show ? SystemColors.Control : SystemColors.Window;
            loadingImage.Visible = show;

        }

    }

}
