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
                return;
            }
            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                MessageBox.Show($"[{i}] {d.Model}");
            }
        }

        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void SetupCustomTitleBar()
        {
            // 그라데이션 그리기 이벤트 연결
            pnlTitleBar.Paint += PnlTitleBar_Paint;

            // 창 드래그 이벤트 연결
            pnlTitleBar.MouseDown += TitleBar_MouseDown;
            lblTitle.MouseDown += TitleBar_MouseDown;

            // 버튼 클릭 이벤트 연결
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

        // [핵심] 제목 표시줄 패널에 그라데이션을 그리는 메서드
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

        // [핵심] 제목 표시줄을 마우스로 끌어 창을 이동시키는 메서드
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
            // webview init
            await webView.EnsureCoreWebView2Async(null);

            // Webview Message Handler 등록
            webView.CoreWebView2.WebMessageReceived += HandleWebMessage;

            // index.html 파일 로드
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
        /*
        private async void HandleWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string jsonMessage = args.WebMessageAsJson;
            if (string.IsNullOrEmpty(jsonMessage)) return;

            JsonNode message = JsonNode.Parse(jsonMessage);
            string messageType = message?["type"]?.GetValue<string>();

            if (messageType == "connectSettings")
            {
                string ip = message?["data"]?["serverIp"]?.GetValue<string>();
                string port = message?["data"]?["serverPort"]?.GetValue<string>();

                signalR = new SignalR(ip, port);
                signalR.MessageReceived += OnMessageReceived;
                signalR.ConnectionStatusChanged += OnConnectionStatusChanged;
                await signalR.ConnectAsync();
            }
        }
        */
        private async void HandleWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            string jsonMessage = args.WebMessageAsJson;
            if (string.IsNullOrEmpty(jsonMessage)) return;

            JsonNode message = JsonNode.Parse(jsonMessage);
            string messageType = message?["type"]?.GetValue<string>();

            // USB 장치 목록 요청 처리
            if (messageType == "requestDeviceList")
            {
                // 최대 5개 장치 정보 반환
                var devices = UsbStorageService.GetAllUsbDevices(5);
                var deviceListJson = JsonSerializer.Serialize(new
                {
                    type = "deviceList",
                    data = devices
                }, new JsonSerializerOptions { IncludeFields = true }); // 필드 포함 옵션 추가
                webView.CoreWebView2.PostWebMessageAsJson(deviceListJson);
                return;
            }

            // "connectSettings" 또는 "connectAndTest" 메시지일 때 서버 정보를 가져옵니다.
            if (messageType == "connectSettings" || messageType == "connectAndTest")
            {
                string ip = message?["data"]?["serverIp"]?.GetValue<string>();
                string port = message?["data"]?["serverPort"]?.GetValue<string>();

                // SignalR 객체를 생성하고 이벤트를 연결합니다.
                signalR = new SignalR(ip, port);
                signalR.MessageReceived += OnMessageReceived;
                signalR.ConnectionStatusChanged += OnConnectionStatusChanged;

                // 서버에 연결을 시도합니다.
                await signalR.ConnectAsync();

                // 만약 메시지 타입이 "connectAndTest"였다면, 테스트 메시지를 즉시 보냅니다.
                if (messageType == "connectAndTest")
                {
                    // 연결이 성공적으로 이루어졌는지 잠시 기다린 후 메시지를 보냅니다.
                    // (연결 상태를 직접 확인하는 로직을 추가하면 더 안정적입니다.)
                    await Task.Delay(1000); // 1초 대기
                    await signalR.SendTestMessageAsync();
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
    }
}