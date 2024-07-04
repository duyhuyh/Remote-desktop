using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.ComponentModel.Com2Interop;
using FireSharp.Config;
using FireSharp.Interfaces;
using FireSharp.Response;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace app
{
    public partial class App : Form
    {

        /*for firebase*/

        IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "O5JDBP85w8WgL0rMajziiWdTxzoilXWYpbaxAzKA",
            BasePath = "https://fir-firebase-winform-default-rtdb.firebaseio.com/"
        };

        IFirebaseClient firebase;

        /*for server*/

        private TcpListener tcpListener;
        private Thread listenThread;
        private List<TcpClient> clients = new List<TcpClient>();
        private bool isMaxDisplay = false;

        /*--------------------------------------------------------*/

        /*for client*/

        private TcpClient client;
        private NetworkStream serverStream;
        private Thread captureThread;
        private Thread commandThread;

        /*--------------------------------------------------------*/

        public App()
        {
            InitializeComponent();
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            firebase = new FireSharp.FirebaseClient(config);
        }

        private void button1_Click(object sender, EventArgs e)
        {

        }

        private string ComputeSHA256Hash(string input)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(input);
                byte[] hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

        private string GetLocalIPv4()
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 &&
                    networkInterface.OperationalStatus == OperationalStatus.Up &&
                    networkInterface.Name.Contains("Wi-Fi"))
                {
                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    foreach (UnicastIPAddressInformation unicastAddress in properties.UnicastAddresses)
                    {
                        if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return unicastAddress.Address.ToString();
                        }
                    }
                }
            }
            return "Không tìm thấy địa chỉ IPv4";
        }

        private string GenerateRandomString(int length)
        {
            const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            RandomNumberGenerator rng = RandomNumberGenerator.Create();
            byte[] randomBytes = new byte[length];
            rng.GetBytes(randomBytes);
            StringBuilder result = new StringBuilder(length);
            foreach (byte b in randomBytes)
            {
                result.Append(validChars[b % validChars.Length]);
            }
            return result.ToString();
        }

        /*-----------------------------SERVER---------------------------------------------------------*/

        private void btnListen_Click(object sender, EventArgs e)
        {
            string Username = txtUSN.Text;
            string Password = txtPSW.Text;
            txtPSW.Enabled = false;
            txtUSN.Enabled = false;
            string Id = ComputeSHA256Hash(Username + Password);
            var data = new Data
            {
                id = Id,
                ipaddress = GetLocalIPv4(),
            };
            firebase.Set("Information/" + Id, data);

            tcpListener = new TcpListener(IPAddress.Any, 5000);
            listenThread = new Thread(new ThreadStart(ListenForClients));
            listenThread.Start();
            listBoxClients.Items.Add("Server started...");
            btnConnect.Enabled = false;
            btnListen.Enabled = false;
            if (!isMaxDisplay)
            {
                WindowState = FormWindowState.Maximized;
                FormBorderStyle = FormBorderStyle.FixedSingle;
                
                isMaxDisplay = true;
            }
        }
        
        private void ListenForClients()
        {
            tcpListener.Start();
            while (true)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                clients.Add(client);
                listBoxClients.Invoke(new Action(() => listBoxClients.Items.Add("Client connected...")));
                
                Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                clientThread.Start(client);
            }
        }

        private void HandleClientComm(object client_obj)
        {
            TcpClient tcpClient = (TcpClient)client_obj;
            NetworkStream clientStream = tcpClient.GetStream();

            while (true)
            {
                byte[] sizeBytes = new byte[4];
                clientStream.Read(sizeBytes, 0, sizeBytes.Length);
                int size = BitConverter.ToInt32(sizeBytes, 0);

                byte[] data = new byte[size];
                int bytesRead = 0;
                while (bytesRead < size)
                {
                    bytesRead += clientStream.Read(data, bytesRead, size - bytesRead);
                }

                using (MemoryStream ms = new MemoryStream(data))
                {
                    pictureBoxDisplay.Image?.Dispose(); // Dispose previous image to release resources
                    pictureBoxDisplay.Image = new Bitmap(ms);
                }
            }

        }

        private void SendCommandToClient(string command)
        {
            foreach (TcpClient client in clients)
            {
                NetworkStream clientStream = client.GetStream();
                byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                clientStream.Write(commandBytes, 0, commandBytes.Length);
            }
        }

        private bool ShiftPress = false;
        private string t = "";
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ControlKey) { t += "^"; }
            else if (e.KeyCode == Keys.Tab) { t += "{TAB}"; }
            else if (e.KeyValue == 18) { t += "%"; }
            else if (e.KeyCode == Keys.ShiftKey) { t += "+"; ShiftPress = true; }
            else if (e.KeyCode.ToString().StartsWith("F") && e.KeyCode.ToString().Length != 1) { t += ("{" + e.KeyCode + "}"); }
            else if (e.Shift && e.KeyCode.ToString() == "D0") { t += "{)}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D1") { t += "{!}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D2") { t += "{@}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D3") { t += "{#}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D4") { t += "{$}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D5") { t += "{%}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D6") { t += "{^}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D7") { t += "{&}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D8") { t += "{*}"; }
            else if (e.Shift && e.KeyCode.ToString() == "D9") { t += "{(}"; }
            else if (e.KeyCode.ToString().StartsWith("NumPad")) { t += e.KeyCode.ToString().Substring(6); }
            else if (e.KeyCode.ToString() == "Up") { t += "{UP}"; }
            else if (e.KeyCode.ToString() == "Down") { t += "{DOWN}"; }
            else if (e.KeyCode.ToString() == "Left") { t += "{LEFT}"; }
            else if (e.KeyCode.ToString() == "Right") { t += "{RIGHT}"; }
            else if (e.KeyCode.ToString() == "Return") { t += "{ENTER}"; }
            else if (e.KeyCode.ToString() == "Capital") { t += "{CAPSLOCK}"; }
            else if (e.KeyCode.ToString() == "Escape") { t += "{ESC}"; }
            else if (e.KeyCode.ToString() == "Back") { t += "{BACKSPACE}"; }
            else if (e.KeyCode.ToString() == "Space") { t += " "; }
            else if (e.KeyCode.ToString() == "NumLock") { t += "{NUMLOCK}"; }
            else if (e.KeyCode.ToString() == "End") { t += "{END}"; }
            else if (e.KeyCode.ToString() == "Home") { t += "{HOME}"; }
            else if (e.KeyCode.ToString() == "Next") { t += "{PGDN}"; }
            else if (e.KeyCode.ToString() == "PageUp") { t += "{PGIP}"; }
            else if (e.KeyCode.ToString() == "Space") { t += "{SPACE}"; }
            else if (e.KeyCode.ToString() == "Delete") { t += "{DELETE}"; }
            else if (e.KeyCode.ToString() == "Pause") { t += "{BREAK}"; }
            else if (e.KeyCode.ToString() == "Insert") { t += "{INSERT}"; }
            else if (e.KeyCode.ToString() == "Break") { t += "{SPACE}"; }
            else if (e.Shift && e.KeyValue == 189) { t += "{_}"; }
            else if (e.Shift && e.KeyValue == 187) { t += "{+}"; }
            else if (e.Shift && e.KeyValue == 219) { t += "{{}"; }
            else if (e.Shift && e.KeyValue == 221) { t += "{}}"; }
            else if (e.Shift && e.KeyValue == 220) { t += "|"; }
            else if (e.Shift && e.KeyValue == 186) { t += "{:}"; }
            else if (e.Shift && e.KeyValue == 222) { t += "\""; }
            else if (e.Shift && e.KeyValue == 188) { t += "<"; }
            else if (e.Shift && e.KeyValue == 190) { t += ">"; }
            else if (e.Shift && e.KeyValue == 191) { t += "?"; }
            else if (e.Shift && e.KeyValue == 192) { t += "{~}"; }
            else if (e.KeyValue == 192) { t += "`"; }
            else if (e.KeyValue == 189) { t += "{-}"; }
            else if (e.KeyValue == 187) { t += "{=}"; }
            else if (e.KeyValue == 219) { t += "["; }
            else if (e.KeyValue == 221) { t += "]"; }
            else if (e.KeyValue == 220) { t += "\\"; }
            else if (e.KeyValue == 186) { t += "{;}"; }
            else if (e.KeyValue == 222) { t += "'"; }
            else if (e.KeyValue == 188) { t += ","; }
            else if (e.KeyValue == 190) { t += "."; }
            else if (e.KeyValue == 191 || e.KeyValue == 111) { t += "/"; }
            else if (e.KeyValue == 106) { t += "*"; }
            else if (e.KeyValue == 109) { t += "-"; }
            else if (e.KeyValue == 107) { t += "{+}"; }
            else if (e.KeyCode.ToString().StartsWith("D") && e.KeyCode.ToString().Length != 1) { t += (e.KeyCode.ToString().Substring(1)); }
            else if (e.Shift && e.KeyValue >= 65 && e.KeyValue <= 90) { t += "+" + e.KeyCode.ToString().ToLower(); }
            else if (e.KeyValue >= 65 && e.KeyValue <= 90) { t += e.KeyCode.ToString().ToLower(); }
            else { t = t + e.KeyCode + " - " + e.KeyValue; }
        }

        private void Form1_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey) { ShiftPress = false; }
            if (t.Length != 0)
            {
                Console.Write(t.Length);
                Console.WriteLine(" - " + t.Replace("++", "+"));
                SendCommandToClient("KEYBOARD:" + t.Replace("++", "+"));
            }
            t = "";
        }


        /*---------------------------------------------------------------------------------------------*/

        /*-----------------------------CLIENT----------------------------------------------------------*/

        private void btnConnect_Click(object sender, EventArgs e)
        {
            string Id = ComputeSHA256Hash(txtUSN.Text + txtPSW.Text);
            FirebaseResponse res = firebase.Get("Information/" + Id);
            Data obj = res.ResultAs<Data>();

            client = new TcpClient(obj.ipaddress, 5000);
            serverStream = client.GetStream();
            KeyPreview = false;
            captureThread = new Thread(new ThreadStart(CaptureScreen));
            captureThread.Start();

            commandThread = new Thread(new ThreadStart(ListenForCommands));
            commandThread.Start();
        }

        private void CaptureScreen()
        {
            while (true)
            {
                Rectangle bound = Screen.PrimaryScreen.Bounds;
                Bitmap screenshot = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb);
                Graphics graphics = Graphics.FromImage(screenshot);
                graphics.CopyFromScreen(0, 0, 0, 0, screenshot.Size, CopyPixelOperation.SourceCopy);

                using (MemoryStream ms = new MemoryStream())
                {
                    ImageCodecInfo jpgEncoder = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    EncoderParameters encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 80L);
                    screenshot.Save(ms, jpgEncoder, encoderParams);

                    byte[] data = ms.ToArray();
                    byte[] sizebyte = BitConverter.GetBytes(data.Length);

                    serverStream.Write(sizebyte, 0, sizebyte.Length);
                    serverStream.Write(data, 0, data.Length);
                }

                Thread.Sleep(100);
            }
        }

        private void ListenForCommands()
        {
            while (true)
            {
                byte[] commandBuffer = new byte[4096];
                int bytesRead = serverStream.Read(commandBuffer, 0, commandBuffer.Length);
                if (bytesRead > 0)
                {
                    string command = Encoding.ASCII.GetString(commandBuffer, 0, bytesRead);
                    ExecuteCommand(command);
                }
            }
        }

        private void ExecuteCommand(string command)
        {
            string[] commandParts = command.Split(':');
            switch (commandParts[0])
            {
                case "KEYBOARD":
                    try 
                    {
                        SendKeys.SendWait(commandParts[1]);
                    } catch 
                    {
                        Console.WriteLine(commandParts[1]);
                    }
                    break;
/*                case "MOVE_MOUSE":
                    int x = int.Parse(commandParts[1]);
                    int y = int.Parse(commandParts[2]);
                    SetCursorPos(x, y);
                    break;
                case "CLICK_MOUSE":
                    if (commandParts[1] == "LEFT")
                    {
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    }
                    break;
                case "SEND_KEYSTROKE":
                    char key = commandParts[1][0];
                    SendKeys.SendWait(key.ToString());
                    break;*/
            }
        }

        private void btnRandom_Click(object sender, EventArgs e)
        {
            string prev = txtPSW.Text;
            string curr = GenerateRandomString(6);
            txtPSW.Text = curr;
            string Id = ComputeSHA256Hash(txtUSN.Text + prev);
            firebase.Delete("Information/" + Id);
            Id = ComputeSHA256Hash(txtUSN.Text + curr);
            var data = new Data
            {
                id = Id,
                ipaddress = GetLocalIPv4(),
            };
            firebase.Set("Information/" + Id, data);
        }

        private void App_FormClosing(object sender, FormClosingEventArgs e)
        {
            firebase.Delete("Information/" + ComputeSHA256Hash(txtUSN.Text + txtPSW.Text));
        }

        /* [DllImport("user32.dll")]
         private static extern bool SetCursorPos(int X, int Y);

         [DllImport("user32.dll")]
         private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

         private const int MOUSEEVENTF_LEFTDOWN = 0x02;
         private const int MOUSEEVENTF_LEFTUP = 0x04;*/

    }
}

