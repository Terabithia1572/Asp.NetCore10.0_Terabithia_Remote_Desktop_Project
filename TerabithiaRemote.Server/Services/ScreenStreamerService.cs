using Microsoft.AspNetCore.SignalR;
using System.Drawing;
using System.Drawing.Imaging;
using TerabithiaRemote.Server.Hubs;
using TerabithiaRemote.Shared.Dtos;
using TerabithiaRemote.Server.Services; // DXGICapture burada

namespace TerabithiaRemote.Server.Services;

public class ScreenStreamerService : BackgroundService
{
    private readonly IHubContext<RemoteHub> _hub;
    private bool _isStreaming = false;
    private readonly DXGICapture _dxgi = new DXGICapture(); // Yeni motorumuz

    public ScreenStreamerService(IHubContext<RemoteHub> _hubContext)
    {
        _hub = _hubContext;
    }

    public void Start() => _isStreaming = true;
    public void Stop() => _isStreaming = false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isStreaming)
            {
                try
                {
                    var dto = CaptureScreenJpeg(70); // %70 Kalite

                    if (dto != null)
                    {
                        // Sadece ekran değiştiğinde veri gönderiyoruz (DXGI sayesinde)
                        await _hub.Clients.All.SendAsync("ScreenFrame", dto, stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Stream Error: {ex.Message}");
                }
            }
            // Bekleme süresini 16ms yaparsak ~60 FPS, 33ms yaparsak ~30 FPS alırız
            await Task.Delay(33, stoppingToken);
        }
    }

    private ScreenFrameDto CaptureScreenJpeg(long quality)
    {
        // DXGI motorundan kareyi iste
        using var bmp = _dxgi.GetNextFrame();

        // Eğer ekran değişmediyse DXGI null döner, biz de null döneriz (İnternet yemez)
        if (bmp == null) return null;

        using var ms = new MemoryStream();
        var codec = GetJpegCodec("image/jpeg");
        var encParams = new EncoderParameters(1);
        encParams.Param[0] = new EncoderParameter(Encoder.Quality, quality);

        bmp.Save(ms, codec, encParams);

        return new ScreenFrameDto
        {
            JpegBytes = ms.ToArray(),
            Width = bmp.Width,
            Height = bmp.Height,
            TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    private ImageCodecInfo GetJpegCodec(string mimeType)
    {
        return ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.MimeType == mimeType);
    }
}