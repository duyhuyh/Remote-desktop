using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Messaging;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ToolBar;

namespace app
{
    public partial class App : Form
    {
        ChatForm chatForm;
        /*for firebase*/

        IFirebaseConfig config = new FirebaseConfig
        {
            AuthSecret = "O5JDBP85w8WgL0rMajziiWdTxzoilXWYpbaxAzKA",
            BasePath = "https://fir-firebase-winform-default-rtdb.firebaseio.com/"
        };

        IFirebaseClient firebase;

        /*for server*/

        private bool isSerRun = false;
        private TcpListener tcpListener;
        private Thread listenThread;
        private List<TcpClient> clients = new List<TcpClient>();
        private bool isMaxDisplay = false;

        private Point initlMousePos;
        private bool isDrag = false;

        /*--------------------------------------------------------*/

        /*for client*/

        private bool isCliRun = false;
        private TcpClient client;
        private NetworkStream serverStream;
        private Thread captureThread;
        private Thread commandThread;

        /*--------------------------------------------------------*/

        public App()
        {
            InitializeComponent();
            CheckForIllegalCrossThreadCalls = false;
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnMenu.Anchor =  AnchorStyles.Right;
            groupBox1.Click += GroupBox_Click;
            pictureBoxDisplay.Click += Form1_Click;
            pictureBoxDisplay.MouseClick += Form1_MouseClick;
            pictureBoxDisplay.MouseDoubleClick += Form1_DoubleClick;
            pictureBoxDisplay.MouseWheel += Form1_MouseWheel;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            firebase = new FireSharp.FirebaseClient(config);
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

        private bool checkFormOpen(string name)
        {
            foreach (Form f in Application.OpenForms)
            {
                if (f.Text == name)
                {
                    return true;
                }
            }
            return false;
        }

        /*-----------------------------SERVER---------------------------------------------------------*/

        private void GroupBox_Click(object sender, EventArgs e) 
        {
            this.KeyPreview = false;
        }

        private void Form1_Click(object sender, EventArgs e)
        {
            this.KeyPreview = true;
        }

        private void Form1_MouseWheel(object sender, MouseEventArgs e)
        {
            Point scrollPosition = e.Location;
            int deltaScroll = e.Delta;
            string cmd;

            if (deltaScroll > 0)
            {
                cmd = $"SCROLL_UP:{scrollPosition.X}:{scrollPosition.Y}";
            }
            else
            {
                cmd = $"SCROLL_DOWN:{scrollPosition.X}:{scrollPosition.Y}";
            }
            SendCommandToClient(cmd);
        }

        private void Form1_DoubleClick(object sender, MouseEventArgs e)
        {
            Point clickPosition = e.Location;
            string cmd = $"DOU_CLICK:{clickPosition.X}:{clickPosition.Y}";
            SendCommandToClient(cmd);
        }

        private void Form1_MouseClick(object sender, MouseEventArgs e)
        {
            listBoxClients.Items.Add(Height + "=" + Width);
            Point clickPosition = e.Location;
            string cmd = $"MOUSE_CLICK:{clickPosition.X}:{clickPosition.Y}";
            if (e.Button == MouseButtons.Left)
            {
                cmd += $":{MouseButtons.Left}";
            }
            else if (e.Button == MouseButtons.Middle)
            {
                cmd += $":{MouseButtons.Middle}";
            }
            else if (e.Button == MouseButtons.Right)
            {
                cmd += $":{MouseButtons.Right}";
            }
          /*  listBoxClients.Items.Add(cmd);*/
            SendCommandToClient(cmd);
        }

        private void btnListen_Click(object sender, EventArgs e)
        {
            if (btnListen.Text == "Listen") 
            {
                if (txtUSN.Text.Length == 0 || txtPSW.Text.Length == 0)
                {
                    MessageBox.Show("Invalid username or password, please try again!");
                    Disconnect();
                }
                else
                {
                    btnListen.Text = "Stop";
                    isSerRun = true;
                    KeyPreview = true;
                    groupBox1.Visible = false;

                    string Username = txtUSN.Text;
                    string Password = txtPSW.Text;
                    txtPSW.Enabled = false;
                    txtUSN.Enabled = false;
                    string Id = ComputeSHA256Hash(Username + Password);
                    var data = new Data
                    {
                        id = Id,
                        ipaddress = GetLocalIPv4(),
                        port = 5000
                    };
                    firebase.Set("Information/" + Id, data);
                    chatForm = new ChatForm("server", GetLocalIPv4(), 5001);
                    chatForm.Show();
                    tcpListener = new TcpListener(IPAddress.Any, 5000);
                    listenThread = new Thread(new ThreadStart(ListenForClients));
                    listenThread.Start();
                    listBoxClients.Items.Add("Server started...");
                    btnConnect.Enabled = false;
                    if (!isMaxDisplay)
                    {
                        
                        pictureBoxDisplay.Height = 768;
                        pictureBoxDisplay.Width = 1366;
                        Width = 1386;
                        Height = 807;
                        FormBorderStyle = FormBorderStyle.FixedSingle;

                        isMaxDisplay = true;
                    }
                }
            }
            else 
            {
                StopServer();
            }
        }
        
        private void ListenForClients()
        {
            tcpListener.Start();
            while (isSerRun)
            {
                try 
                {
                    TcpClient client = tcpListener.AcceptTcpClient();
                    clients.Add(client);
                    listBoxClients.Invoke(new Action(() => listBoxClients.Items.Add("Client connected...")));

                    Thread clientThread = new Thread(new ParameterizedThreadStart(HandleClientComm));
                    clientThread.Start(client);
                    SendCommandToClient("SUCCESS");
                }
                catch (Exception) 
                {
                    StopServer();
                }
            }
        }

        private void HandleClientComm(object client_obj)
        {
            TcpClient tcpClient = (TcpClient)client_obj;
            NetworkStream clientStream = tcpClient.GetStream();
            try 
            {
                while (isSerRun)
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
                        pictureBoxDisplay.Image?.Dispose();
                        pictureBoxDisplay.Image = new Bitmap(ms);
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Client disconnected!");
            }
            finally
            {
                tcpClient.Close();
                clients.Remove(tcpClient);
                tcpListener.Stop();
                listenThread.Abort();
                chatForm.StopListening();
                chatForm.Close();
                this.Invoke(new Action(() =>
                {
                    if (clients.Count == 0)
                    {
                        if (groupBox1.Visible == false)
                            groupBox1.Visible = true;
                        pictureBoxDisplay.Image?.Dispose();
                        pictureBoxDisplay.Image = null;
                        listBoxClients.Items.Clear();
                        isSerRun = false;
                        KeyPreview = false;
                        txtPSW.Enabled = true;
                        txtUSN.Enabled = true;
                        btnConnect.Enabled = true;
                        btnListen.Text = "Listen";
                    }
                }));
            }

        }

        private void StopServer()
        {
            chatForm.StopListening();
            chatForm.Close();
            SendCommandToClient("CHAT:SERVER IS STOP!");
            if (tcpListener != null)
            {
                tcpListener.Stop();
            }

            foreach (var client in clients)
            {
                client.Close();
            }

            clients.Clear();

            if (listenThread != null && listenThread.IsAlive)
            {
                listenThread.Abort();
            }
            listBoxClients.Invoke(new Action(() => listBoxClients.Items.Clear()));
            this.Invoke(new Action(() =>
            {
                if (groupBox1.Visible == false)
                    groupBox1.Visible = true;
                isSerRun = false;
                KeyPreview = false;
                txtPSW.Enabled = true;
                txtUSN.Enabled = true;
                btnConnect.Enabled = true;
                btnListen.Text = "Listen";
                pictureBoxDisplay.Image?.Dispose();
                pictureBoxDisplay.Image = null; 
            }));
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
            if (btnConnect.Text == "Connect")
            {
                btnConnect.Text = "Disconnect";
                isCliRun = true;

                string Id = ComputeSHA256Hash(txtUSN.Text + txtPSW.Text);
                FirebaseResponse res = firebase.Get("Information/" + Id);
                Data obj = res.ResultAs<Data>();
                if (obj == null)
                {
                    MessageBox.Show("Can't connect to orther PC, please try again!");
                    Disconnect();
                }
                else 
                {
                    chatForm = new ChatForm("client", obj.ipaddress, obj.port + 1);
                    chatForm.Show();
                    client = new TcpClient(obj.ipaddress, obj.port);
                    serverStream = client.GetStream();

                    btnListen.Enabled = false;
                    KeyPreview = false;

                    captureThread = new Thread(new ThreadStart(CaptureScreen));
                    captureThread.Start();

                    commandThread = new Thread(new ThreadStart(ListenForCommands));
                    commandThread.Start();

                }
            }
            else 
            {
                Disconnect();
            }
        }

        private void CaptureScreen()
        {
            while (isCliRun)
            {
                try
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
                catch (Exception) 
                {
                    Disconnect();
                    break;
                }
            }
        }

        private void ListenForCommands()
        {
            while (isCliRun)
            {
                try 
                {
                    byte[] commandBuffer = new byte[4096];
                    int bytesRead = serverStream.Read(commandBuffer, 0, commandBuffer.Length);
                    if (bytesRead > 0)
                    {
                        string command = Encoding.ASCII.GetString(commandBuffer, 0, bytesRead);
                        ExecuteCommand(command);
                    }
                }
                catch 
                {
                    Disconnect();
                    break;
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
                case "MOUSE_CLICK":
                    int x = int.Parse(commandParts[1]);
                    int y = int.Parse(commandParts[2]);
                    SetCursorPos(x, y);
                    if (commandParts[3] == MouseButtons.Left.ToString())
                    {
                        mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
                    }
                    else if (commandParts[3] == MouseButtons.Right.ToString())
                    {
                        mouse_event(MOUSEEVENTF_RIGHTDOWN | MOUSEEVENTF_RIGHTUP, 0, 0, 0, 0);
                    }
                    else if (commandParts[3] == MouseButtons.Middle.ToString())
                    {
                        mouse_event(MOUSEEVENTF_MIDDLEDOWN | MOUSEEVENTF_MIDDLEUP, 0, 0, 0, 0);
                    }
                    break;
                case "DOU_CLICK":
                    int X = int.Parse(commandParts[1]);
                    int Y = int.Parse(commandParts[2]);
                    SetCursorPos(X, Y);
                    mouse_event(MOUSEEVENTF_LEFTDOWN | MOUSEEVENTF_LEFTUP | MOUSEEVENTF_LEFTDBLCLK, 0, 0, 0, 0);
                    break;
                case "SCROLL_UP":
                    int scrollUpX = int.Parse(commandParts[1]);
                    int scrollUpY = int.Parse(commandParts[2]);
                    SetCursorPos(scrollUpX, scrollUpY);
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, 120, 0);
                    break;
                case "SCROLL_DOWN":
                    int scrollDownX = int.Parse(commandParts[1]);
                    int scrollDownY = int.Parse(commandParts[2]);
                    SetCursorPos(scrollDownX, scrollDownY);
                    mouse_event(MOUSEEVENTF_WHEEL, 0, 0, -120, 0);
                    break;
                case "CHAT":
                    listBoxClients.Invoke(new Action(() => listBoxClients.Items.Add("Server is stop!")));
                    if (checkFormOpen("ChatForm"))
                    {
                        chatForm.StopListening();
                        chatForm.Close();
                    }
                    break;
                case "SUCCESS":
                    listBoxClients.Invoke(new Action(() => listBoxClients.Items.Add("Connect succesfull!")));
                    break;
            }
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

        private const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const int MOUSEEVENTF_LEFTUP = 0x0004;
        private const int MOUSEEVENTF_LEFTDBLCLK = 0x0008;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const int MOUSEEVENTF_RIGHTUP = 0x0010;
        private const int MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const int MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const int MOUSEEVENTF_WHEEL = 0x0800;
        private const int MOUSEEVENTF_MOVE = 0x0001;

        private void Disconnect()
        {
            if (checkFormOpen("ChatForm"))
            {
                chatForm.StopListening();
                chatForm.Close();
            }

            isCliRun = false;

            if (serverStream != null)
            {
                serverStream.Close();
            }

            if (client != null)
            {
                client.Close();
            }

            if (captureThread != null && captureThread.IsAlive)
            {
                captureThread.Abort();
            }

            if (commandThread != null && commandThread.IsAlive)
            {
                commandThread.Abort();
            }
            listBoxClients.Invoke(new Action(() => listBoxClients.Items.Clear()));
            this.Invoke(new Action(() =>
            {
                KeyPreview = false;
                btnConnect.Text = "Connect";
                btnListen.Enabled = true;
            }));
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
            if (isSerRun) StopServer();
            if (isCliRun) Disconnect();
            Application.Exit();
        }

        private void btnMenu_Click(object sender, EventArgs e)
        {
            if (groupBox1.Visible == false)
                groupBox1.Visible = true;
            else groupBox1.Visible = false;
        }
    }
}

