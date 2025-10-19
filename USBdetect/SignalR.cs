using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;
using System.Windows.Forms;

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
            // "Test Client"라는 사용자로 "This is a test message."라는 메시지를 보냅니다.
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
    }
}
