using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UltraView
{
    public partial class Update : Form
    {
        public Update()
        {
            InitializeComponent();
        }


        class InternetConnection
        {
            [DllImport("wininet.dll")]
            private extern static bool InternetGetConnectedState(out int description, int reservedValuine);
            public static bool IsConnectedToInternet()
            {
                int desc;
                return InternetGetConnectedState(out desc, 0);
            }
        }
        string UltraView = @"UltraView";                // create source folder to store all programs and updates
        string updatetxt = @"UltraView\update.txt";    // create a .txt file to write new update version code
        string currenttxt = @"UltraView\current.txt";  // create a .txt file to write current version code in use
        string apppath = @"UltraView";          // Create folder to store your extracted program.
        string AppLink = "";
        string InstallLink = "";


        string updatetxtlink = "";

        readonly string cai1 = Application.StartupPath + @"\cai.exe"; // file path in the same folder as the program
        readonly string cai2 = @"UltraView\app\cai.exe";     // copy path to the startup folder
        private void Update_Load(object sender, EventArgs e)
        {
            if (Directory.Exists(UltraView))
            {
                // create current.txt file
                StreamWriter st = new StreamWriter(currenttxt);

                st.Close();

            }
            else
            {
                Directory.CreateDirectory(UltraView);


            }
            //

            // check internet connection
            if (InternetConnection.IsConnectedToInternet())
            {
                // download new update code


                WebClient ud = new WebClient();
                // MessageBox.Show("...");

                ud.DownloadFileCompleted += new AsyncCompletedEventHandler(udcom);
                Uri update = new Uri(updatetxtlink);
                ud.DownloadFileAsync(update, updatetxt);

            }
            else
                MessageBox.Show("Unable to check for updates due to lack of internet connection");
        }
        // download completion of the update.txt file
        private void udcom(object sender, AsyncCompletedEventArgs e)
        {

            // read this file into label 2
            
            //StreamReader rd = new StreamReader(currenttxt);
            //label1.Text = rd.ReadLine();
            //rd.Close();
            StreamReader st = new StreamReader(updatetxt);
            label2.Text = st.ReadLine();
            st.Close();
            // check the version to notify for update
            if (label1.Text == label2.Text)
            {
                // if equal, there is no update
                MessageBox.Show("No new update available");
            }
            else // if not equal
            {

                // notify new update and new version
                DialogResult di = MessageBox.Show("New update version " + label2.Text + " is ready to install",
                    "New Update Available", MessageBoxButtons.YesNo);
                if (di == DialogResult.Yes) // if yes is selected, update
                {
                    Process myProcess = new Process();

                    try
                    {
                        // true is the default, but it is important not to set it to false 
                        myProcess.StartInfo.UseShellExecute = true;
                        myProcess.StartInfo.FileName = AppLink;
                        myProcess.Start();
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("Can not download update file");
                    }
                }
                
            }
        }
    }
}