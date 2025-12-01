using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows.Forms;

using UsbLibWrapper;

namespace USBdetect
{
    public partial class Main : Form
    {
        private SignalR signalR;

        public Main()
        {
            InitializeComponent();
            this.Load += Load_Main;
            this.FormClosing += Main_FormClosing;

            // notifyIcon1.DoubleClick += new EventHandler(notifyIcon1_MouseDoubleClick);
            manage_NotifyIcon();

            SetupCustomTitleBar();

            var manager = new UsbManagerManaged();
            var devices = manager.Scan();
            if (devices.Count == 0)
            {
                MessageBox.Show("No USB devices found.");
            }
            else
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    var d = devices[i];
                    MessageBox.Show($"[{i}] {d.Model}");
                }
            }

            // 앱 시작 시 스캔 결과를 서버로 보낼 준비(연결 후 실제 전송)
            _ = SendDeviceListToServerAsync(devices);
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void SetupCustomTitleBar()
        {
            pnlTitleBar.Paint += PnlTitleBar_Paint;
            pnlTitleBar.MouseDown += TitleBar_MouseDown;
            lblTitle.MouseDown += TitleBar_MouseDown;

            btnClose.Click += (s, e) => this.Close();
            btnMinimize.Click += (s, e) => this.WindowState = FormWindowState.Minimized;
            btnMaximize.Click += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Normal)
                    this.WindowState = FormWindowState.Maximized;
                else
                    this.WindowState = FormWindowState.Normal;
            };
        }

        private void PnlTitleBar_Paint(object sender, PaintEventArgs e)
        {
            Rectangle rect = pnlTitleBar.ClientRectangle;
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddRectangle(rect);
                using (PathGradientBrush pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(255, 255, 255);
                    pgb.SurroundColors = new Color[] { Color.FromArgb(45, 55, 72) };
                    pgb.CenterPoint = new PointF(0, 0);
                    e.Graphics.FillRectangle(pgb, rect);
                }
            }
        }

        private void TitleBar_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private async void Load_Main(object sender, EventArgs e)
        {
            await webView.EnsureCoreWebView2Async(null);

            webView.CoreWebView2.WebMessageReceived += HandleWebMessage;

            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
            webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);
        }

        private void webView_Click(object sender, EventArgs e)
        {
            if (sender is Control ctl)
            {
                try { ctl.Focus(); } catch { }
            }
        }
        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (signalR != null)
            {
                _ = signalR.DisconnectAsync();
            }
        }

        private async void HandleWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string jsonMessage = args.WebMessageAsJson;
            if (string.IsNullOrEmpty(jsonMessage)) return;

            JsonNode message = JsonNode.Parse(jsonMessage);
            string messageType = message?["type"]?.GetValue<string>();

            if (messageType == "requestDeviceList")
            {
                var devices = UsbStorageService.GetAllUsbDevices(5);
                var deviceListJson = JsonSerializer.Serialize(new
                {
                    type = "deviceList",
                    data = devices
                }, new JsonSerializerOptions { IncludeFields = true });
                webView.CoreWebView2.PostWebMessageAsJson(deviceListJson);

                // 서버로도 전송
                await SendDeviceListToServerAsync(devices);
                return;
            }

            if (messageType == "connectSettings" || messageType == "connectAndTest")
            {
                string ip = message?["data"]?["serverIp"]?.GetValue<string>();
                string port = message?["data"]?["serverPort"]?.GetValue<string>();

                signalR = new SignalR(ip, port);
                signalR.MessageReceived += OnMessageReceived;
                signalR.ConnectionStatusChanged += OnConnectionStatusChanged;

                await signalR.ConnectAsync();

                if (messageType == "connectAndTest")
                {
                    await Task.Delay(1000);
                    await signalR.SendTestMessageAsync();

                    var devices = UsbStorageService.GetAllUsbDevices(50);
                    await SendDeviceListToServerAsync(devices);
                }
            }
        }

        private void OnMessageReceived(string user, string message)
        {
            this.Invoke((MethodInvoker)delegate
            {
                var receivedMessage = $"{user}: {message}";
                MessageBox.Show(this, receivedMessage, "서버로부터 받은 메시지");
            });
        }

        private void OnConnectionStatusChanged(string status)
        {
            this.Invoke((MethodInvoker)delegate
            {
                MessageBox.Show(this, status, "연결 상태 변경");
            });
        }

        private void manage_NotifyIcon()
        {
            string startupPath = Application.StartupPath;
            string strIconFilePath = System.IO.Path.Combine(startupPath, "favicon.ico");

            if (System.IO.File.Exists(strIconFilePath))
            {
                notifyIcon1.Icon = new System.Drawing.Icon(strIconFilePath);
                notifyIcon1.Text = "USB Detector";
                notifyIcon1.Visible = true;
            }
        }

        private void notifyIcon1_MouseDoubleClick(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Normal;

            notifyIcon1.ShowBalloonTip(3000, "[주의]", "새로운 USB 입력이 감지되었습니다!", ToolTipIcon.Warning);
        }

        // 서버로 장치 목록을 SignalR을 통해 전송
        private async Task SendDeviceListToServerAsync(object devices)
        {
            try
            {
                if (signalR == null)
                    return;

                var payload = new
                {
                    type = "deviceList",
                    data = devices
                };

                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { IncludeFields = true });

                // 허브 메서드 "SendDeviceList"로 JSON 문자열 전달
                await signalR.SendDeviceListAsync(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to send device list: {ex}");
            }
        }
    }
}