using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;

namespace UltraView
{
    public partial class ChatForm : Form
    {
        TcpClient client;
        TcpListener server;
        private readonly Thread Listening;
        private readonly Thread GetText;
        private int port; //Port used to send image is different 
        private byte type;
        private string ip;
        public ChatForm(byte Type, string IP, int Port) //Type = 0 is server, 1 is client
        {

            type = Type;
            port = Port;
            ip = IP;
            if (type == 0)//server
            {
                client = new TcpClient();
                Listening = new Thread(StartListening);
                GetText = new Thread(ReceiveText);
            }
            else //type=1 , client
            {
                client = new TcpClient();
                GetText = new Thread(ReceiveText);
            }
            InitializeComponent();
            Writelogfile("OpenChatForm" + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
        }
        protected override void OnLoad(EventArgs e)
        {

            base.OnLoad(e);
            if (type == 0)
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
        //Handle connection and disconnection
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
                // MessageBox.Show("Listening failed!");
                StopListening();
            }

        }
        public void StopListening()
        {
            try
            {
                client.Close();
                client = null;
                server.Stop();
            }
            catch { }
            try
            {
                if (GetText.IsAlive) GetText.Abort();
                if (Listening.IsAlive) Listening.Abort();
            }
            catch { }
            // MessageBox.Show("Disconnect success!");
        }

        //Receive messages
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
                            tbxShowMessage.Text += "\nThey: " + str.Substring(3);
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
            Writelogfile("FormChatClose" + DateTime.Now.ToLongDateString() + " " + DateTime.Now.ToLongTimeString());
        }

        //Send messages
        private NetworkStream stream;

        private void btnSend_Click_1(object sender, EventArgs e)
        {
            if (tbxMessage.Text != "")
            {
                try
                {
                    sendText("MS:" + tbxMessage.Text);
                    Console.WriteLine("Message sent successfully: MS:" + tbxMessage.Text); // Logging message sent
                }
                catch
                {
                    tbxShowMessage.Text += "\nMessage not sent!";
                    Console.WriteLine("Error: Message not sent!"); // Logging error message
                    return;
                }

                tbxShowMessage.Text += "\nWe: " + tbxMessage.Text;
                Console.WriteLine("UI updated with message: We: " + tbxMessage.Text); // Logging UI update

                tbxMessage.Clear();
                tbxMessage.Focus();
            }
            else
            {
                Console.WriteLine("No message to send."); // Logging no message to send
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

        //WriteLog
        private void Writelogfile(string txt)
        {
            Console.WriteLine($"Writing to log file: {txt}");
            using (FileStream fs = new FileStream(@"log.txt", FileMode.Append))
            {
                using (StreamWriter writer = new StreamWriter(fs, Encoding.UTF8))
                {
                    writer.WriteLine(txt);
                }
            }
        }
        private void ChatForm_Load(object sender, EventArgs e)
        {
            tbxMessage.Focus();
            tbxShowMessage.SelectionColor = Color.Blue;


        }
    }
}