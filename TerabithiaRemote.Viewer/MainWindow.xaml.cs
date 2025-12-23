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
        private int _lastFrameWidth;
        private int _lastFrameHeight;
        private int _lastNormX;
        private int _lastNormY;
        private int _lastFrameW = 1920;
        private int _lastFrameH = 1080;
        private int _remoteScreenW = 1920;
        private int _remoteScreenH = 1080;


        public MainWindow()
        {
            InitializeComponent();
        }


        private System.Windows.Rect GetRenderedImageRect(System.Windows.Controls.Image image)
        {
            if (image.Source == null || image.ActualWidth <= 0 || image.ActualHeight <= 0)
                return System.Windows.Rect.Empty;

            double controlW = image.ActualWidth;
            double controlH = image.ActualHeight;

            // Source width/height (WPF ImageSource pixels)
            double sourceW = image.Source.Width;
            double sourceH = image.Source.Height;

            if (sourceW <= 0 || sourceH <= 0)
                return System.Windows.Rect.Empty;

            // Uniform scale
            double scale = Math.Min(controlW / sourceW, controlH / sourceH);
            double renderedW = sourceW * scale;
            double renderedH = sourceH * scale;

            double offsetX = (controlW - renderedW) / 2.0;
            double offsetY = (controlH - renderedH) / 2.0;

            return new System.Windows.Rect(offsetX, offsetY, renderedW, renderedH);
        }

        private bool TryMapToRemotePoint(System.Windows.Point pInImageControl, out int rx, out int ry)
        {
            rx = ry = 0;

            var rect = GetRenderedImageRect(ImgScreen);
            if (rect == System.Windows.Rect.Empty) return false;
            if (!rect.Contains(pInImageControl)) return false;

            double nx = (pInImageControl.X - rect.X) / rect.Width;   // 0..1
            double ny = (pInImageControl.Y - rect.Y) / rect.Height;  // 0..1

            rx = (int)Math.Round(nx * (_remoteScreenW - 1));
            ry = (int)Math.Round(ny * (_remoteScreenH - 1));

            if (rx < 0) rx = 0;
            if (ry < 0) ry = 0;
            if (rx >= _remoteScreenW) rx = _remoteScreenW - 1;
            if (ry >= _remoteScreenH) ry = _remoteScreenH - 1;

            return true;
        }

        private bool TryMapToFrameNormalized(System.Windows.Point p, out int normX, out int normY)
        {
            normX = 0; normY = 0;

            if (_lastFrameWidth <= 0 || _lastFrameHeight <= 0) return false;
            if (ImgScreen.ActualWidth <= 1 || ImgScreen.ActualHeight <= 1) return false;

            // Image control içinde Uniform fit hesapla (letterbox alanlarını çıkar)
            double controlW = ImgScreen.ActualWidth;
            double controlH = ImgScreen.ActualHeight;

            double imgW = _lastFrameWidth;
            double imgH = _lastFrameHeight;

            double scale = Math.Min(controlW / imgW, controlH / imgH);
            double displayW = imgW * scale;
            double displayH = imgH * scale;

            double offsetX = (controlW - displayW) / 2.0;
            double offsetY = (controlH - displayH) / 2.0;

            double xOnImage = p.X - offsetX;
            double yOnImage = p.Y - offsetY;

            // Letterbox dışındaysa gönderme
            if (xOnImage < 0 || yOnImage < 0 || xOnImage >= displayW || yOnImage >= displayH)
                return false;

            // Görüntü üstündeki gerçek piksele çevir
            double pixelX = xOnImage / scale;
            double pixelY = yOnImage / scale;

            // 0..65535 normalize
            normX = (int)Math.Round(pixelX * 65535.0 / Math.Max(1, (_lastFrameWidth - 1)));
            normY = (int)Math.Round(pixelY * 65535.0 / Math.Max(1, (_lastFrameHeight - 1)));

            // clamp
            normX = Math.Max(0, Math.Min(65535, normX));
            normY = Math.Max(0, Math.Min(65535, normY));

            return true;
        }


        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _connection = new HubConnectionBuilder()
                    .WithUrl(HubUrl)
                    .WithAutomaticReconnect()
                    .Build();

                _connection.On<string>("ServerHello", msg =>
                {
                    Dispatcher.Invoke(() => TxtStatus.Text = msg);
                });

                _connection.On<ScreenFrameDto>("ScreenFrame", dto =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        // Remote ekran boyutunu frame’den alıyoruz
                        _remoteScreenW = dto.Width;
                        _remoteScreenH = dto.Height;

                        ImgScreen.Source = JpegToBitmapSource(dto.JpegBytes);
                        TxtStatus.Text = $"Frame: {dto.Width}x{dto.Height} @ {dto.TimestampUnixMs}";
                    });
                });




                await _connection.StartAsync();
                TxtStatus.Text = "Connected to hub.";
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Connect error: " + ex.Message;
            }
        }
        private async void ImgScreen_MouseMove(object sender, MouseEventArgs e)
        {
            if (_connection == null) return;

            var p = e.GetPosition(ImgScreen);
            if (!TryMapToRemotePoint(p, out int rx, out int ry)) return;

            await _connection.InvokeAsync("SendMouseInput",
                new MouseInputDto { X = rx, Y = ry, Action = MouseAction.Move });
        }


        private async void ImgScreen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_connection == null) return;

            var p = e.GetPosition(ImgScreen);
            if (!TryMapToRemotePoint(p, out int rx, out int ry)) return;

            var action = e.ChangedButton == MouseButton.Left
                ? MouseAction.LeftDown
                : MouseAction.RightDown;

            await _connection.InvokeAsync("SendMouseInput",
                new MouseInputDto { X = rx, Y = ry, Action = action });
        }


        private async void ImgScreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_connection == null) return;

            var p = e.GetPosition(ImgScreen);
            if (!TryMapToRemotePoint(p, out int rx, out int ry)) return;

            var action = e.ChangedButton == MouseButton.Left
                ? MouseAction.LeftUp
                : MouseAction.RightUp;

            await _connection.InvokeAsync("SendMouseInput",
                new MouseInputDto { X = rx, Y = ry, Action = action });
        }




        protected override async void OnKeyDown(KeyEventArgs e)
        {
            if (_connection == null) return;

            await _connection.InvokeAsync("SendKeyboardInput",
                new KeyboardInputDto
                {
                    VirtualKeyCode = KeyInterop.VirtualKeyFromKey(e.Key),
                    IsKeyDown = true
                });
        }

        protected override async void OnKeyUp(KeyEventArgs e)
        {
            if (_connection == null) return;

            await _connection.InvokeAsync("SendKeyboardInput",
                new KeyboardInputDto
                {
                    VirtualKeyCode = KeyInterop.VirtualKeyFromKey(e.Key),
                    IsKeyDown = false
                });
        }


        private async void BtnStartStream_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var url = ServerBaseUrl + "/api/stream/start";
                TxtStatus.Text = "Starting stream...";
                _ = Task.Run(async () => await _http.GetAsync(url));
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "StartStream error: " + ex.Message;
            }
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
