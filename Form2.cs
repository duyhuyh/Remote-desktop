using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace app
{
    public partial class ChatForm : Form
    {
        TcpClient client;
        TcpListener server;
        private readonly Thread Listening = null;
        private readonly Thread GetText = null;
        private string type;
        private int port;
        private string ip;
        public ChatForm(string Type, string Ip, int Port)
        {
            type = Type;
            ip = Ip;
            port = Port;
            if (type == "server")
            {
                client = new TcpClient();
                Listening = new Thread(StartListening);
                GetText = new Thread(ReceiveText);
            }
            else
            {
                client = new TcpClient();
                GetText = new Thread(ReceiveText);
            }
            InitializeComponent();
        }
        protected override void OnLoad(EventArgs e)
        {

            base.OnLoad(e);
            if (type == "server")
            {
                server = new TcpListener(IPAddress.Any, port);
                Listening.Start();

            }
            else
            {
                try
                {
                    client.Connect(ip, port);
                    GetText.Start();
                }
                catch (Exception)
                {
                }
            }
        }

        private void StartListening()
        {
            try
            {
                while (!client.Connected)
                {
                    server.Start();
                    client = server.AcceptTcpClient();
                }
                GetText.Start();
            }
            catch
            {
                StopListening();
            }

        }

        public void StopListening()
        {
            try
            {
                if (client != null) 
                {
                    client.Close();
                    client = null;
                }
                if (server != null)
                    server.Stop();
            }
            catch { }
            try
            {
                if (GetText.IsAlive && GetText != null) GetText.Abort();
                if (type == "server") Listening.Abort();
            }
            catch { }
        }

        private NetworkStream istream;
        private void ReceiveText()
        {
            BinaryFormatter binFormatter = new BinaryFormatter();
            try
            {
                while (client.Connected)
                {
                    try
                    {
                        istream = client.GetStream();
                        string str = (String)binFormatter.Deserialize(istream);
                        string result = str.Substring(0, 3);
                        if (result == "MS:")
                        {
                            listBox1.Invoke(new Action(() => listBox1.Items.Add("\nThey: " + str.Substring(3))));
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            StopListening();
        }

        //Gửi tin nhắn
        private NetworkStream stream;

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (txt.Text != "")
            {
                try
                {
                    sendText("MS:" + txt.Text);
                }
                catch
                {
                    listBox1.Invoke(new Action(() => listBox1.Items.Add("Tin nhắn không gửi được!")));
                    return;
                }

                listBox1.Invoke(new Action(() => listBox1.Items.Add("\nWe: " + txt.Text)));
                txt.Clear();
                txt.Focus();
            }

        }
        private void sendText(string str)
        {
            if (client.Connected)
            {
                BinaryFormatter binFormatter = new BinaryFormatter();
                stream = client.GetStream();
                binFormatter.Serialize(stream, str);
            }

        }
        private void ChatForm_Load(object sender, EventArgs e)
        {
            txt.Focus();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

    }
}
