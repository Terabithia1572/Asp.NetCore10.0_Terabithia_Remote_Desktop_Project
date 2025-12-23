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


        public MainWindow()
        {
            InitializeComponent();
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
                        _lastFrameWidth = dto.Width;
                        _lastFrameHeight = dto.Height;

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

            var pos = e.GetPosition(ImgScreen);

            int vw = (int)ImgScreen.ActualWidth;
            int vh = (int)ImgScreen.ActualHeight;
            if (vw <= 0 || vh <= 0) return;

            var dto = new MouseInputDto
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                ViewWidth = vw,
                ViewHeight = vh,
                Action = MouseAction.Move
            };

            await _connection.InvokeAsync("SendMouseInput", dto);
        }



        private async void ImgScreen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_connection == null) return;

            var pos = e.GetPosition(ImgScreen);

            int vw = (int)ImgScreen.ActualWidth;
            int vh = (int)ImgScreen.ActualHeight;
            if (vw <= 0 || vh <= 0) return;

            var action = e.ChangedButton == MouseButton.Left
                ? MouseAction.LeftDown
                : MouseAction.RightDown;

            await _connection.InvokeAsync("SendMouseInput", new MouseInputDto
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                ViewWidth = vw,
                ViewHeight = vh,
                Action = action
            });
        }


        private async void ImgScreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_connection == null) return;

            var pos = e.GetPosition(ImgScreen);

            int vw = (int)ImgScreen.ActualWidth;
            int vh = (int)ImgScreen.ActualHeight;
            if (vw <= 0 || vh <= 0) return;

            var action = e.ChangedButton == MouseButton.Left
                ? MouseAction.LeftUp
                : MouseAction.RightUp;

            await _connection.InvokeAsync("SendMouseInput", new MouseInputDto
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                ViewWidth = vw,
                ViewHeight = vh,
                Action = action
            });
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
