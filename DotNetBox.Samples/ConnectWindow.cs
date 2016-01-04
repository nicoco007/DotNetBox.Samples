using System.Windows.Forms;

namespace DotNetBox.Samples
{

    public partial class ConnectWindow : Form
    {

        public ConnectWindow()
        {

            InitializeComponent();

            AcceptButton = submitButton;

        }

        public new string ShowDialog()
        {
            
            base.ShowDialog();

            return codeTextBox.Text;

        }

        private void submitButton_Click(object sender, System.EventArgs e)
        {

            this.Close();

        }

    }

}
