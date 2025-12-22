using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Drawing;
using System.Drawing.Imaging;
using TerabithiaRemote.Server.Hubs;
using TerabithiaRemote.Shared.Dtos;

namespace TerabithiaRemote.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StreamController : ControllerBase
    {
        private readonly IHubContext<RemoteHub> _hub;

        public StreamController(IHubContext<RemoteHub> hub)
        {
            _hub = hub;
        }

        [HttpGet("start")]
        public async Task<IActionResult> Start()
        {
            // Basit MVP: istek gelince 10 fps gibi ekran yayınla
            // (daha sonra session + stop + token ekleyeceğiz)
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    var dto = CaptureScreenJpeg(70); // kalite 1-100
                    await _hub.Clients.All.SendAsync("ScreenFrame", dto);
                    await Task.Delay(100); // 10 fps
                }
            });

            return Ok("Streaming started");
        }

        private static ScreenFrameDto CaptureScreenJpeg(long quality)
        {
            // Primary screen snapshot
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

            using (var bmp = new Bitmap(bounds.Width, bounds.Height))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size);
                }

                using (var ms = new MemoryStream())
                {
                    var jpgEncoder = ImageCodecInfo.GetImageEncoders().First(x => x.FormatID == ImageFormat.Jpeg.Guid);
                    var encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);

                    bmp.Save(ms, jpgEncoder, encParams);

                    return new ScreenFrameDto
                    {
                        JpegBytes = ms.ToArray(),
                        Width = bounds.Width,
                        Height = bounds.Height,
                        TimestampUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                    };
                }
            }
        }
    }
}
