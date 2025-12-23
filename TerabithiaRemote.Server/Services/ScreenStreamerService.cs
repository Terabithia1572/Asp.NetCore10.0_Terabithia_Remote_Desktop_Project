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
                    // Tüm bağlı client'lara gönder
                    await _hub.Clients.All.SendAsync("ScreenFrame", dto, stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Capture Error: {ex.Message}");
                }
            }

            // FPS'e göre bekleme süresi (1000ms / 10fps = 100ms)
            await Task.Delay(1000 / _fps, stoppingToken);
        }
    }

    private ScreenFrameDto CaptureScreenJpeg(long quality)
    {
        // Senin yazdığın GDI+ yakalama mantığını buraya taşıdık
        var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

        using var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);
            DrawCursor(g); // İmleci çizme metodun (aşağıda)

            using var ms = new MemoryStream();
            var codec = GetJpegCodec();
            var encParams = new EncoderParameters(1);
            encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

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