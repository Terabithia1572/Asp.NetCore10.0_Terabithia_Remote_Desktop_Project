using Microsoft.AspNetCore.Mvc;
using TerabithiaRemote.Server.Services;

[ApiController]
[Route("api/[controller]")]
public class StreamController : ControllerBase
{
    private readonly ScreenStreamerService _streamer;

    public StreamController(ScreenStreamerService streamer)
    {
        _streamer = streamer;
    }

    // StreamController.cs içindeki ilgili satırları şu şekilde değiştir:

    [HttpGet("start")]
    public IActionResult Start()
    {
        _streamer.Start(); // SetStreaming(true) yerine Start() kullanıyoruz
        return Ok("Streaming started");
    }

    [HttpGet("stop")]
    public IActionResult Stop()
    {
        _streamer.Stop(); // SetStreaming(false) yerine Stop() kullanıyoruz
        return Ok("Streaming stopped");
    }
}