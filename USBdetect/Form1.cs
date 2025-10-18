using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.AspNetCore.SignalR.Client; // [추가] SignalR 클라이언트 라이브러리
using System.Threading.Tasks;                 // [추가] 비동기 작업을 위해 필요
using Newtonsoft.Json;                      // [추가] JSON 직렬화를 위한 라이브러리

namespace USBdetect
{
    public partial class Form1 : Form
    {
        // [추가] SignalR 서버와의 연결을 관리하는 HubConnection 객체
        private HubConnection connection;

        public Form1()
        {
            InitializeComponent();

            // -------------------------------------------------------------------
            // [병합] SignalR 연결 설정 추가
            // -------------------------------------------------------------------

            connection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/chathub") // ★★★ 이 주소를 확인하고 수정하세요 ★★★
                .Build();

            // 서버로부터 "ReceiveMessage" 메시지를 받았을 때 처리할 동작을 정의합니다.
            connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                // UI 스레드에서 컨트롤을 안전하게 업데이트하기 위해 Invoke를 사용합니다.
                this.Invoke((Action)(() =>
                {
                    var receivedMessage = $"{user}: {message}";

                    // TODO: 받은 메시지를 표시할 컨트롤에 추가하세요.
                    // 예: lstMessages.Items.Add(receivedMessage);
                    MessageBox.Show(receivedMessage, "서버로부터 받은 메시지"); // 임시로 메시지 박스 사용
                }));
            });


            // 기존 이벤트 핸들러 등록
            this.Load += Form1_Load;

            // [추가] 폼이 닫힐 때 SignalR 연결을 안전하게 종료하기 위한 이벤트 핸들러 등록
            this.FormClosing += Form1_FormClosing;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            // 1. 기존 WebView2 초기화 (순서 유지)
            await webView.EnsureCoreWebView2Async(null);
            webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            string htmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "index.html");
            webView.CoreWebView2.Navigate(new Uri(htmlPath).AbsoluteUri);

            // 2. [병합] SignalR 서버에 비동기적으로 연결
            try
            {
                await connection.StartAsync();
                // TODO: 연결 성공 메시지를 표시할 컨트롤에 추가하세요.
                // 예: lstMessages.Items.Add("서버에 연결되었습니다.");
            }
            catch (Exception ex)
            {
                // TODO: 연결 실패 메시지를 표시할 컨트롤에 추가하세요.
                // 예: lstMessages.Items.Add($"연결 오류: {ex.Message}");
                MessageBox.Show($"SignalR 서버 연결 실패: {ex.Message}");
            }
        }

        // JavaScript(HTML)에서 보낸 메시지를 처리하는 기존 메서드
        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string messageFromJs = e.TryGetWebMessageAsString();

            // C# 메시지 박스를 띄우는 기존 코드
            MessageBox.Show($"WebView로부터 메시지를 받았습니다: {messageFromJs}", "WinForm");

            // [병합] WebView에서 받은 메시지를 SignalR 서버로 전송
            // "WinForm User"는 임의의 사용자 이름입니다. 필요에 맞게 수정하세요.
            _ = SendMessageToServerAsync("WinForm User", messageFromJs);
        }

        // [추가] 메시지를 SignalR 서버로 비동기 전송하는 메서드
        public async Task SendMessageToServerAsync(string user, string message)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                try
                {
                    // 서버의 "SendMessage" 메서드를 호출합니다.
                    await connection.InvokeAsync("SendMessage", user, message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"메시지 전송 오류: {ex.Message}");
                }
            }
            else
            {
                MessageBox.Show("서버에 연결되어 있지 않습니다.");
            }
        }

        // [추가] 폼이 닫힐 때 SignalR 연결을 안전하게 종료하는 메서드
        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (connection != null)
            {
                await connection.DisposeAsync();
            }
        }

        // 기존 webView_Click 이벤트 핸들러 (수정 없음)
        private void webView_Click(object sender, EventArgs e)
        {
            if (sender is Control ctl)
            {
                try { ctl.Focus(); } catch { }
            }
        }
    }
}