using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Server
{
    public partial class MainWindow : Window
    {
        private TcpListener _server;
        private CancellationTokenSource _cancellationTokenSource;
        private List<TcpClient> _clients = new List<TcpClient>();
        private Random _random = new Random();
        private readonly string[] _computerResponses = { "Привет!", "Как дела?", "До свидания!", "Отличная погода!" };
        private readonly string[] _humanResponses = { "Здравствуй!", "Как ты?", "До свидания!", "Погода отличная!" };

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string ipAddress = IpAddressTextBox.Text;
                int port = int.Parse(PortTextBox.Text);

                _server = new TcpListener(IPAddress.Parse(ipAddress), port);
                _server.Start();

                _cancellationTokenSource = new CancellationTokenSource();

                StatusLabel.Content = $"Сервер запущен на {ipAddress}:{port}";
                MessagesTextBox.AppendText("Сервер запущен...\n");

                await AcceptClientsAsync(_cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка запуска сервера: {ex.Message}");
            }
        }

        private async Task AcceptClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _server.AcceptTcpClientAsync();
                _clients.Add(client);
                MessagesTextBox.AppendText("Новый клиент подключился\n");
                _ = HandleClientAsync(client, cancellationToken);
            }
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
        {
            var stream = client.GetStream();
            var buffer = new byte[1024];

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                try
                {
                    int byteCount = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (byteCount > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                        Dispatcher.Invoke(() => MessagesTextBox.AppendText($"Клиент: {message}\n"));

                        if (message.Trim().ToLower() == "bye")
                        {
                            Dispatcher.Invoke(() => MessagesTextBox.AppendText("Клиент завершил соединение\n"));
                            break;
                        }

                        await RespondToClientAsync(stream, message);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => MessagesTextBox.AppendText($"Ошибка: {ex.Message}\n"));
                }
            }

            client.Close();
            _clients.Remove(client);
        }

        private async Task RespondToClientAsync(NetworkStream stream, string clientMessage)
        {
            string response;
            string mode = ModeComboBox.SelectedItem.ToString();

            if (mode.Contains("Человек — компьютер"))
            {
                // Сервер отвечает как "человек" в режиме Человек — компьютер
                response = _humanResponses[_random.Next(_humanResponses.Length)];
            }
            else if (mode.Contains("Компьютер — человек"))
            {
                // Сервер отвечает как "компьютер" в режиме Компьютер — человек
                response = _computerResponses[_random.Next(_computerResponses.Length)];
            }
            else if (mode.Contains("Компьютер — компьютер"))
            {
                // Сервер отвечает как "компьютер" в режиме Компьютер — компьютер
                response = _computerResponses[_random.Next(_computerResponses.Length)];
            }
            else
            {
                response = "Неизвестный режим";
            }

            var responseBytes = Encoding.UTF8.GetBytes(response);
            await stream.WriteAsync(responseBytes, 0, responseBytes.Length);

            Dispatcher.Invoke(() => MessagesTextBox.AppendText($"Сервер: {response}\n"));
        }

        private void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            string message = SendMessageTextBox.Text;

            if (!string.IsNullOrWhiteSpace(message))
            {
                foreach (var client in _clients)
                {
                    var stream = client.GetStream();
                    var messageBytes = Encoding.UTF8.GetBytes(message);
                    stream.Write(messageBytes, 0, messageBytes.Length);
                }

                MessagesTextBox.AppendText($"Сервер: {message}\n");
                SendMessageTextBox.Clear();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _cancellationTokenSource?.Cancel();
            _server?.Stop();

            foreach (var client in _clients)
            {
                client.Close();
            }
        }
    }
}