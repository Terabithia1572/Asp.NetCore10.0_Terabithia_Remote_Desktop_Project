using System;

namespace TerabithiaRemote.Shared.Dtos
{
    public class ScreenFrameDto
    {
        public byte[] JpegBytes { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public long TimestampUnixMs { get; set; }
    }
}
