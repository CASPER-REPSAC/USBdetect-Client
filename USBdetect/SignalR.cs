using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace USBdetect
{
    public class SignalR
    {
        private HubConnection connection;

        public event Action<string, string> MessageReceived;
        public event Action<string> ConnectionStatusChanged;

        public SignalR(string ip, string port)
        {
            string serverUrl = $"http://{ip}:{port}/chathub";

            connection = new HubConnectionBuilder()
                .WithUrl(serverUrl)
                .Build();

            connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                MessageReceived?.Invoke(user, message);
            });

            connection.Closed += async (error) =>
            {
                ConnectionStatusChanged?.Invoke("Connection closed.");
                await Task.Delay(new Random().Next(0, 5) * 1000);
                await ConnectAsync();
            };
        }

        public async Task ConnectAsync()
        {
            if (connection.State == HubConnectionState.Disconnected)
            {
                try
                {
                    await connection.StartAsync();
                    ConnectionStatusChanged?.Invoke("Server connected successfully.");
                }
                catch (Exception ex)
                {
                    ConnectionStatusChanged?.Invoke($"Connection failed: {ex.Message}");
                }
            }
        }

        public async Task SendMessageAsync(string user, string message)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                try
                {
                    await connection.InvokeAsync("SendMessage", user, message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending message: {ex.Message}");
                }
            }
        }

        public async Task SendTestMessageAsync()
        {
            await SendMessageAsync("Test Client", "This is a test message.");
        }

        public async Task DisconnectAsync()
        {
            if (connection != null && connection.State != HubConnectionState.Disconnected)
            {
                await connection.DisposeAsync();
                ConnectionStatusChanged?.Invoke("Disconnected.");
            }
        }

        // JSON 문자열(장치 목록 등)을 서버 허브로 전송
        // 서버 허브 메서드 이름은 "SendDeviceList"로 가정합니다.
        public async Task SendDeviceListAsync(string jsonPayload)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                try
                {
                    // 서버 허브: Task SendDeviceList(string json)
                    await connection.InvokeAsync("SendDeviceList", jsonPayload);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error sending device list: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("SignalR connection is not established. Skipping SendDeviceList.");
            }
        }
    }
}
