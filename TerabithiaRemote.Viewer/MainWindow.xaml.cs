using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using TerabithiaRemote.Shared.Dtos;
using MouseAction = TerabithiaRemote.Shared.Dtos.MouseAction;

namespace TerabithiaRemote.Viewer
{
    public partial class MainWindow : Window
    {
        private HubConnection _connection;
        private readonly HttpClient _http = new HttpClient();

        private const string ServerBaseUrl = "https://localhost:7135";
        private const string HubUrl = ServerBaseUrl + "/remoteHub";

        private int _remoteScreenW = 1920;
        private int _remoteScreenH = 1080;
        private DateTime _lastMouseSent = DateTime.MinValue;

        public MainWindow()
        {
            InitializeComponent();
            // Program açılır açılmaz sunucuya bağlanıp ID'mizi alalım
            _ = InitializeConnectionAsync();
        }

        private async Task InitializeConnectionAsync()
        {
            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl(HubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _connection.ServerTimeout = TimeSpan.FromSeconds(30);

                // 1. Kendi ID'mizi sunucudan alıyoruz
                _connection.On<string>("ServerInfo", id =>
                {
                    Dispatcher.Invoke(() => {
                        TxtMyId.Text = id;
                        TxtStatus.Text = "Your ID is ready. Enter Target ID and click Connect.";
                    });
                });

                // 2. Karşıya bağlanma durumunu dinle
                _connection.On<bool>("JoinStatus", success =>
                {
                    Dispatcher.Invoke(() => {
                        TxtStatus.Text = success ? "Connected to Target! You can Start Stream now." : "Target ID not found!";
                    });
                });

                // 3. Ekran görüntülerini dinle
                _connection.On<ScreenFrameDto>("ScreenFrame", dto =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _remoteScreenW = dto.Width;
                        _remoteScreenH = dto.Height;
                        ImgScreen.Source = JpegToBitmapSource(dto.JpegBytes);
                        TxtStatus.Text = $"Streaming: {dto.Width}x{dto.Height}";
                    });
                });

                await _connection.StartAsync();
                TxtStatus.Text = "Connected to Server. Fetching ID...";
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => TxtStatus.Text = "Init Error: " + ex.Message);
            }
        }

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            // Bağlantı zaten InitializeConnectionAsync içinde kuruldu, burada sadece JoinSession diyeceğiz
            if (_connection == null || _connection.State != HubConnectionState.Connected)
            {
                TxtStatus.Text = "Not connected to server yet!";
                return;
            }

            string targetId = TxtTargetId.Text.Trim();
            if (string.IsNullOrEmpty(targetId))
            {
                TxtStatus.Text = "Please enter a Target ID!";
                return;
            }

            try
            {
                TxtStatus.Text = $"Joining Session: {targetId}...";
                await _connection.InvokeAsync("JoinSession", targetId);
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Join Error: " + ex.Message;
            }
        }

        // --- MOUSE & KEYBOARD EVENTLERİ (Senin kodların aynen kalıyor) ---

        private async void ImgScreen_MouseMove(object sender, MouseEventArgs e)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            if ((DateTime.Now - _lastMouseSent).TotalMilliseconds < 33) return;

            var p = e.GetPosition(ImgScreen);
            if (!TryMapToRemotePoint(p, out int rx, out int ry)) return;

            _lastMouseSent = DateTime.Now;
            await _connection.InvokeAsync("SendMouseInput", new MouseInputDto { X = rx, Y = ry, Action = MouseAction.Move });
        }

        private async void ImgScreen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            var p = e.GetPosition(ImgScreen);
            if (!TryMapToRemotePoint(p, out int rx, out int ry)) return;

            var action = e.ChangedButton == MouseButton.Left ? MouseAction.LeftDown : MouseAction.RightDown;
            await _connection.InvokeAsync("SendMouseInput", new MouseInputDto { X = rx, Y = ry, Action = action });
        }

        private async void ImgScreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            var p = e.GetPosition(ImgScreen);
            if (!TryMapToRemotePoint(p, out int rx, out int ry)) return;

            var action = e.ChangedButton == MouseButton.Left ? MouseAction.LeftUp : MouseAction.RightUp;
            await _connection.InvokeAsync("SendMouseInput", new MouseInputDto { X = rx, Y = ry, Action = action });
        }

        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("SendKeyboardInput", new KeyboardInputDto { VirtualKeyCode = KeyInterop.VirtualKeyFromKey(e.Key), IsKeyDown = true });
        }

        protected override async void OnKeyUp(KeyEventArgs e)
        {
            if (_connection?.State != HubConnectionState.Connected) return;
            await _connection.InvokeAsync("SendKeyboardInput", new KeyboardInputDto { VirtualKeyCode = KeyInterop.VirtualKeyFromKey(e.Key), IsKeyDown = false });
        }

        // --- YARDIMCI METODLAR (Mapleme ve Resim Çevirme) ---

        private bool TryMapToRemotePoint(System.Windows.Point pInImageControl, out int rx, out int ry)
        {
            rx = ry = 0;
            var rect = GetRenderedImageRect(ImgScreen);
            if (rect == System.Windows.Rect.Empty || !rect.Contains(pInImageControl)) return false;

            double nx = (pInImageControl.X - rect.X) / rect.Width;
            double ny = (pInImageControl.Y - rect.Y) / rect.Height;

            rx = (int)Math.Round(nx * (_remoteScreenW - 1));
            ry = (int)Math.Round(ny * (_remoteScreenH - 1));
            return true;
        }

        private System.Windows.Rect GetRenderedImageRect(System.Windows.Controls.Image image)
        {
            if (image.Source == null || image.ActualWidth <= 0 || image.ActualHeight <= 0) return System.Windows.Rect.Empty;
            double scale = Math.Min(image.ActualWidth / image.Source.Width, image.ActualHeight / image.Source.Height);
            double renderedW = image.Source.Width * scale;
            double renderedH = image.Source.Height * scale;
            return new System.Windows.Rect((image.ActualWidth - renderedW) / 2, (image.ActualHeight - renderedH) / 2, renderedW, renderedH);
        }

        private async void BtnStartStream_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TxtStatus.Text = "Starting stream...";
                await _http.GetAsync(ServerBaseUrl + "/api/stream/start");
            }
            catch (Exception ex) { TxtStatus.Text = "Error: " + ex.Message; }
        }

        private static BitmapSource JpegToBitmapSource(byte[] jpegBytes)
        {
            using (var ms = new MemoryStream(jpegBytes))

            {
                var img = new BitmapImage();
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = ms;
                img.EndInit();
                img.Freeze();
                return img;
            }
        }
    }
}