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

        public MainWindow()
        {
            InitializeComponent();
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

            var dto = new MouseInputDto
            {
                X = (int)pos.X,
                Y = (int)pos.Y,
                Action = MouseAction.Move
            };

            await _connection.InvokeAsync("SendMouseInput", dto);
        }
        private async void ImgScreen_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_connection == null) return;

            var action = e.ChangedButton == MouseButton.Left
                ? MouseAction.LeftDown
                : MouseAction.RightDown;

            await _connection.InvokeAsync("SendMouseInput",
                new MouseInputDto { Action = action });
        }

        private async void ImgScreen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_connection == null) return;

            var action = e.ChangedButton == MouseButton.Left
                ? MouseAction.LeftUp
                : MouseAction.RightUp;

            await _connection.InvokeAsync("SendMouseInput",
                new MouseInputDto { Action = action });
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
