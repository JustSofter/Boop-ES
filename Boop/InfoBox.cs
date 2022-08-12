using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Boop
{
    partial class InfoBox : Form
    {
        public InfoBox()
        {
            InitializeComponent();
        }



        private void InfoBox_Load(object sender, EventArgs e)
        {
            this.Text = "Boop-ES // Robichani6-1 + JustSofter";
            label1.Text = "v1.0-ES";
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void lblSnekFriendly_Click(object sender, EventArgs e)
        {

        }
    }
}
