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

    [HttpGet("start")]
    public IActionResult Start()
    {
        _streamer.SetStreaming(true);
        return Ok("Streaming started");
    }

    [HttpGet("stop")]
    public IActionResult Stop()
    {
        _streamer.SetStreaming(false);
        return Ok("Streaming stopped");
    }
}