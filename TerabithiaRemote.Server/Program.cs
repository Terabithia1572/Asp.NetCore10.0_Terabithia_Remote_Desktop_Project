using TerabithiaRemote.Server.Hubs;
using TerabithiaRemote.Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();
// Servisi hem BackgroundService hem de dýþarýdan eriþilebilir sýnýf olarak kaydet
builder.Services.AddSingleton<ScreenStreamerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScreenStreamerService>());

// Viewer baþka porttan baðlanacaðý için CORS þart
builder.Services.AddCors(options =>
{
    options.AddPolicy("cors", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .SetIsOriginAllowed(_ => true)
         .AllowCredentials());
});

var app = builder.Build();

app.UseHttpsRedirection();

app.UseCors("cors");

app.MapControllers();
app.MapHub<RemoteHub>("/remoteHub");

app.Run();
