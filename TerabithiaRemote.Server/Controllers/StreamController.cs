using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
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
            var bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;

            using (var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb))
            using (var g = Graphics.FromImage(bmp))
            {
                // Ekranı yakala
                g.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

                // Cursor'ı bitmap'in üstüne çiz
                DrawCursor(g);

                // JPEG'e çevir
                using (var ms = new MemoryStream())
                {
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
        }

        private static ImageCodecInfo GetJpegCodec()
        {
            return ImageCodecInfo.GetImageEncoders().First(x => x.MimeType == "image/jpeg");
        }

        // ---------------- CURSOR OVERLAY ----------------

        private static void DrawCursor(Graphics g)
        {
            var ci = new CURSORINFO();
            ci.cbSize = Marshal.SizeOf(ci);

            if (!GetCursorInfo(ref ci)) return;
            if (ci.flags != CURSOR_SHOWING) return;

            var pt = ci.ptScreenPos;

            // Cursor ikonunu çiz
            DrawIconEx(
                g.GetHdc(),
                pt.x,
                pt.y,
                ci.hCursor,
                0,
                0,
                0,
                IntPtr.Zero,
                DI_NORMAL
            );

            g.ReleaseHdc();
        }

        private const int CURSOR_SHOWING = 0x00000001;
        private const int DI_NORMAL = 0x0003;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CURSORINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hCursor;
            public POINT ptScreenPos;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorInfo(ref CURSORINFO pci);

        [DllImport("user32.dll")]
        private static extern bool DrawIconEx(
            IntPtr hdc,
            int xLeft,
            int yTop,
            IntPtr hIcon,
            int cxWidth,
            int cyWidth,
            int istepIfAniCur,
            IntPtr hbrFlickerFreeDraw,
            int diFlags);
    }
}
