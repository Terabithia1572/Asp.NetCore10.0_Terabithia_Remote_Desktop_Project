using Microsoft.AspNetCore.SignalR;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using TerabithiaRemote.Server.Hubs;
using TerabithiaRemote.Shared.Dtos;

namespace TerabithiaRemote.Server.Services;

public class ScreenStreamerService : BackgroundService
{
    private readonly IHubContext<RemoteHub> _hub;
    private bool _isStreaming = false;
    private int _fps = 10; // İstersen bunu artırabiliriz
    private string _lastFrameHash = string.Empty;

    public ScreenStreamerService(IHubContext<RemoteHub> hub)
    {
        _hub = hub;
    }

    // Bu metod dışarıdan (Controller'dan) çağrılacak
    public void SetStreaming(bool status) => _isStreaming = status;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isStreaming)
            {
                try
                {
                    var dto = CaptureScreenJpeg(70);

                    // HASH KONTROLÜ: Görüntü değişti mi?
                    string currentHash = ComputeHash(dto.JpegBytes);

                    if (currentHash != _lastFrameHash)
                    {
                        await _hub.Clients.All.SendAsync("ScreenFrame", dto, stoppingToken);
                        _lastFrameHash = currentHash;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Capture Error: {ex.Message}");
                }
            }
            await Task.Delay(100, stoppingToken);
        }
    }

    // Hızlı bir şekilde byte dizisinin hash'ini alan yardımcı metod
    private string ComputeHash(byte[] data)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        byte[] hash = md5.ComputeHash(data);
        return Convert.ToBase64String(hash);
    }

    private ScreenFrameDto CaptureScreenJpeg(long quality)
    {
        var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

        // PERFORMANS İPUCU: Ekranın tam boyutunu değil, %75 veya %50'sini gönderebiliriz.
        // Şimdilik tam boy kalsın ama ilerde bir ayar (Scale) ekleyebiliriz.
        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            // GDI+ yakalama
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

            // İmleci çiz
            DrawCursor(g);

            using var ms = new MemoryStream();
            var codec = GetJpegCodec();

            // JPEG kalitesini ve hızını optimize eden parametreler
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

            // Ekstra: Eğer performans çok düşerse görüntüyü küçültüp kaydedebilirsin.
            bmp.Save(ms, codec, encParams);

            return new ScreenFrameDto
            {
                JpegBytes = ms.ToArray(),
                Width = bounds.Width,
                Height = bounds.Height,
                TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
        }
    }

    private ImageCodecInfo GetJpegCodec() =>
        ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");

    // Cursor çizme mantığını buraya aldık (DllImport'lar sınıf içinde olmalı)
    private void DrawCursor(Graphics g)
    {
        var ci = new CURSORINFO { cbSize = Marshal.SizeOf(typeof(CURSORINFO)) };
        if (GetCursorInfo(ref ci) && ci.flags == 0x00000001)
        {
            DrawIconEx(g.GetHdc(), ci.ptScreenPos.x, ci.ptScreenPos.y, ci.hCursor, 0, 0, 0, IntPtr.Zero, 0x0003);
            g.ReleaseHdc();
        }
    }

    #region WinAPI
    [StructLayout(LayoutKind.Sequential)]
    struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)]
    struct CURSORINFO { public int cbSize, flags; public IntPtr hCursor; public POINT ptScreenPos; }
    [DllImport("user32.dll")] static extern bool GetCursorInfo(ref CURSORINFO pci);
    [DllImport("user32.dll")] static extern bool DrawIconEx(IntPtr hdc, int xLeft, int yTop, IntPtr hIcon, int cxWidth, int cyWidth, int istepIfAniCur, IntPtr hbrFlickerFreeDraw, int diFlags);
    #endregion
}